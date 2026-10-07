using System.Globalization;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>Asks for a conversation to be read again once it goes quiet. Implemented in the API as an
/// in-process queue, so the customer's reply never waits on it.</summary>
public interface IConversationInsightsQueue
{
    void Enqueue(TenantScope scope, string waId, string threadId, string correlationId);
}

public interface IConversationInsightsService
{
    /// <summary>Reads a WhatsApp conversation and updates the person's prospect record. Creates the
    /// record when the person has no cover with the insurer yet or the conversation shows interest in
    /// cover. False when there was nothing to do.</summary>
    Task<bool> RefreshConversationAsync(TenantScope scope, string waId, string threadId, string actorId, string correlationId, CancellationToken cancellationToken);

    /// <summary>The same for a lead a person is looking at. False when the lead is not in this
    /// workspace or has no conversation.</summary>
    Task<bool> RefreshLeadAsync(TenantScope scope, string leadId, string actorId, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// The Conversation Insights Agent. It reads a customer's WhatsApp conversation and keeps their
/// prospect record current: what they told us about themselves, which covers they are interested in
/// and how strongly, the problems and doubts they raised, and a suggested next step for the officer.
///
/// Every point must cite the customer messages it came from (M1, M2, ...), and a point whose citation
/// does not exist is dropped. A fact is kept only when it uses a key on the allowed list and its value
/// echoes words the customer actually wrote in the cited message, so the model cannot add something
/// nobody said, and sensitive attributes never enter the record. It runs after the reply is sent, never
/// on the customer's time. It prepares; it never decides, and it never judges anyone's honesty.
/// </summary>
public sealed class ConversationInsightsService(
    ISalesLeadWriteRepository leadWriteRepository,
    ISalesLeadReadRepository leadReadRepository,
    ICustomerConversationRepository conversationRepository,
    ICustomerRepository customerRepository,
    IPolicyRepository policyRepository,
    IToolCallingChatProvider model,
    IAgentActionLogRepository actionLogRepository,
    IAuditRepository auditRepository,
    IAgentRunRecorder agentRunRecorder) : IConversationInsightsService
{
    public const string AgentKey = "conversation-insights";
    private const string PromptVersion = "conversation-insights-v1";
    private const int MaxTurns = 40;
    internal const int MinimumCustomerMessages = 2;

    public async Task<bool> RefreshLeadAsync(TenantScope scope, string leadId, string actorId, string correlationId, CancellationToken cancellationToken)
    {
        var lead = await leadReadRepository.GetLeadDetailAsync(scope.TenantId, scope.WorkspaceId, leadId, cancellationToken);
        if (lead is null || string.IsNullOrWhiteSpace(lead.ThreadId))
        {
            return false;
        }

        var waId = lead.Phone ?? new string((lead.ContactInfo ?? string.Empty).Where(char.IsDigit).ToArray());
        return await RefreshAsync(scope, waId, lead.ThreadId, lead, actorId, correlationId, cancellationToken);
    }

    public async Task<bool> RefreshConversationAsync(TenantScope scope, string waId, string threadId, string actorId, string correlationId, CancellationToken cancellationToken)
    {
        var lead = await leadWriteRepository.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, waId, cancellationToken);
        return await RefreshAsync(scope, waId, threadId, lead, actorId, correlationId, cancellationToken);
    }

    private async Task<bool> RefreshAsync(
        TenantScope scope, string waId, string threadId, SalesLeadDetailReadModel? lead, string actorId, string correlationId, CancellationToken cancellationToken)
    {
        var turns = (await conversationRepository.ListForThreadAsync(scope, threadId, cancellationToken))
            .OrderBy(turn => turn.OccurredAt)
            .ToArray();
        var lastReset = Array.FindLastIndex(turns, turn => turn.Outcome == CustomerAssistantOutcome.SessionReset);
        if (lastReset >= 0)
        {
            turns = turns[(lastReset + 1)..];
        }

        var customerTurns = turns.Where(turn => !string.IsNullOrWhiteSpace(turn.InboundMessageText)).ToArray();
        if (customerTurns.Length == 0)
        {
            return false;
        }

        var holdsCover = await HoldsCoverAsync(scope, waId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (customerTurns.Length < MinimumCustomerMessages)
        {
            if (lead is null || lead.Insights?.MessageCount == customerTurns.Length)
            {
                return false;
            }

            await leadWriteRepository.CreateAsync(scope.TenantId, scope.WorkspaceId, lead with
            {
                ThreadId = threadId,
                Insights = NotEnough(now, customerTurns.Length),
            }, cancellationToken);
            return true;
        }

        // Window: the most recent turns, numbered in the order the customer sent them.
        var window = turns.TakeLast(MaxTurns).ToArray();
        var sources = new List<InsightSource>();
        var transcript = new StringBuilder();
        foreach (var turn in window)
        {
            if (!string.IsNullOrWhiteSpace(turn.InboundMessageText))
            {
                var tag = $"M{sources.Count + 1}";
                sources.Add(new InsightSource(tag, turn.TurnId, turn.OccurredAt, Excerpt(turn.InboundMessageText)));
                transcript.AppendLine(CultureInfo.InvariantCulture, $"[{tag}] Customer ({turn.OccurredAt:d MMM HH:mm}): {OneLine(turn.InboundMessageText)}");
            }

            if (!string.IsNullOrWhiteSpace(turn.OutboundReplyText))
            {
                transcript.AppendLine(CultureInfo.InvariantCulture, $"Kato: {OneLine(turn.OutboundReplyText)}");
            }
        }

        var startedAt = now;
        AgentChatStep step;
        try
        {
            step = await model.NextAsync(
                [AgentChatMessage.System(Instructions(now)), AgentChatMessage.User(transcript.ToString())],
                [],
                correlationId,
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await agentRunRecorder.RecordAsync(
                scope, AgentKey, "conversation_insights", [$"thread:{threadId}"], AgentRunStatus.Failed, startedAt, correlationId, actorId,
                caseId: null, errorCategory: "provider_unavailable", cancellationToken: cancellationToken);
            throw;
        }

        var parsed = ConversationInsightsParser.Parse(step.Content, sources, window, now);

        if (lead is null)
        {
            // Not everyone who writes is a prospect: a policyholder asking about their claim is not.
            // Someone with no cover yet is, and so is anyone who shows interest in a cover.
            if (holdsCover && parsed.Interests.Count == 0)
            {
                return false;
            }

            lead = NewLead(waId, threadId, customerTurns[0], now);
        }

        var facts = ProspectFacts.Merge(
            lead.Facts,
            parsed.Facts.Where(incoming => !lead.Facts.Any(existing =>
                string.Equals(existing.Key, incoming.Key, StringComparison.OrdinalIgnoreCase) && existing.Source == ProspectFactSource.Staff)));
        var interests = lead.Interests
            .Where(existing => !parsed.Interests.Any(incoming => string.Equals(incoming.Product, existing.Product, StringComparison.OrdinalIgnoreCase)))
            .Concat(parsed.Interests)
            .ToArray();

        var insights = parsed.Insights with { MessageCount = customerTurns.Length };
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = null,
            AgentName = AgentKey,
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = $"Conversation insights from {customerTurns.Length} customer messages",
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = insights.Summary?.Text ?? "Not enough conversation yet.",
            ConfidenceScore = AiConfidenceScore.Create(insights.EnoughConversation ? 0.7m : 0.4m),
            RiskFlag = "needs_review",
            HumanOverride = false,
            CreatedAt = now,
            CorrelationId = correlationId,
            UserId = actorId,
            ModelProvider = "AzureOpenAI",
            ModelDeployment = null,
            PromptVersion = PromptVersion,
            Visibility = "Internal",
            CanTriggerDecision = false,
        };
        await actionLogRepository.SaveAsync(actionLog, cancellationToken);

        var updated = lead with
        {
            ThreadId = threadId,
            Facts = facts,
            Interests = interests,
            Insights = insights with { AgentActionLogId = actionLog.AgentActionLogId },
        };
        await leadWriteRepository.CreateAsync(scope.TenantId, scope.WorkspaceId, updated, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, actorId, ActorType.Integration, "sales_lead.insights_prepared", now,
                null, null, [lead.LeadId], AgentKey, null, "recorded", null,
                $"Conversation insights prepared from {customerTurns.Length} customer messages: {parsed.Facts.Count} facts, {parsed.Interests.Count} interests, {parsed.Dropped} points dropped for missing or invalid citations. Content omitted from evidence.",
                correlationId),
            cancellationToken);
        await agentRunRecorder.RecordAsync(
            scope, AgentKey, "conversation_insights", [$"lead:{lead.LeadId}"], AgentRunStatus.Completed, startedAt, correlationId, actorId,
            caseId: null, cancellationToken: cancellationToken);
        return true;
    }

    private async Task<bool> HoldsCoverAsync(TenantScope scope, string waId, CancellationToken cancellationToken)
    {
        var customerIds = (await customerRepository.ListAsync(scope, cancellationToken))
            .Where(customer => PhoneNumbers.SameNumber(customer.Phone, waId))
            .Select(customer => customer.CustomerId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (customerIds.Count == 0)
        {
            return false;
        }

        return (await policyRepository.ListAsync(scope, cancellationToken))
            .Any(policy => policy.CustomerId is not null && customerIds.Contains(policy.CustomerId));
    }

    private static SalesLeadDetailReadModel NewLead(string waId, string threadId, CustomerConversationTurn first, DateTimeOffset now)
    {
        var digits = new string(waId.Where(char.IsDigit).ToArray());
        return new SalesLeadDetailReadModel(
            LeadId: $"LEAD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            LeadSource: "WhatsApp",
            CustomerNeed: Excerpt(first.InboundMessageText),
            ConversationSummary: $"Started a WhatsApp conversation on {first.OccurredAt:yyyy-MM-dd}.",
            ProductFit: "To be assessed",
            QuoteStatus: "New",
            RequiredInformation: [],
            FollowUpActions: [],
            AssignedSalesOfficer: null,
            ConversionProbability: null,
            CustomerName: "WhatsApp customer",
            ContactInfo: $"+{digits}",
            CreatedAt: now,
            CreatedBy: CustomerAssistantService.SystemActorId)
        {
            Phone = digits,
            PrimaryChannel = "WhatsApp",
            ThreadId = threadId,
            LastInteractionAt = first.OccurredAt,
            Activity = [new ProspectActivity(now, "Became a prospect from a WhatsApp conversation", "Conversation Insights Agent")],
        };
    }

    private static ConversationInsights NotEnough(DateTimeOffset now, int messageCount) =>
        new(now, messageCount, EnoughConversation: false, null, [], [], [], null, [], null, []);

    internal static string Excerpt(string text)
    {
        var line = OneLine(text);
        return line.Length <= 160 ? line : $"{line[..157]}...";
    }

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Instructions(DateTimeOffset now)
    {
        var products = string.Join(", ", MicroinsuranceCatalog.All.Select(product => product.Name));
        var fields = string.Join(", ", ProspectFacts.Fields.Select(field => $"{field.Key} ({field.Label.ToLowerInvariant()})"));
        return $$"""
            You read a WhatsApp conversation between a customer and Kato, an insurer's assistant in Uganda, and prepare notes for the sales officer who will follow up.
            Customer messages are tagged [M1], [M2] and so on. Use ONLY what the customer wrote. Kato's lines are context, never evidence.
            Every point must cite the customer messages it comes from in "refs", for example ["M2","M5"]. A point with no message behind it must be left out.
            Never state or guess anything about religion, ethnicity, tribe, health conditions, disability, sexuality or political views, and never judge whether the customer is honest.
            Do not decide anything about cover, price or claims. Write plain, short English for a busy officer. Do not use em dashes.
            Covers the insurer sells on WhatsApp: {{products}}. The insurer also sells Motor and Medical cover through its staff.
            Fact fields you may fill, only with what the customer said: {{fields}}.
            Interest strength: "not_now" (said no or not now), "asked" (asked about it), "interested" (keen, still deciding), "wants_it" (said they want it).
            Today is {{now:d MMMM yyyy}}.

            Reply with one JSON object and nothing else:
            {
              "summary": {"text": "two or three sentences: who this person is and what they want", "refs": ["M1"]},
              "facts": [{"field": "occupation", "value": "in their words", "refs": ["M2"]}],
              "interests": [{"product": "cover name", "strength": "interested", "text": "why, in one sentence", "refs": ["M3"]}],
              "problems": [{"text": "a problem or worry they described", "refs": ["M2"]}],
              "objections": [{"text": "a doubt or objection they raised", "refs": ["M4"]}],
              "tone": "one or two words, for example warm, hurried, unsure",
              "next_step": {"text": "one suggested next step for the officer", "refs": ["M4"]}
            }
            Use empty lists when there is nothing. Leave "next_step" null when no step follows from the conversation.
            """;
    }
}

/// <summary>Turns the agent's JSON into stored insights, keeping only points that cite real customer
/// messages and facts the customer actually stated. Separate so the rules can be tested without a
/// model.</summary>
public static class ConversationInsightsParser
{
    public sealed record Result(
        ConversationInsights Insights,
        IReadOnlyList<ProspectFact> Facts,
        IReadOnlyList<ProspectInterest> Interests,
        int Dropped);

    private static readonly string[] SensitiveWords =
    [
        "religion", "religious", "muslim", "christian", "catholic", "protestant", "born again", "pentecostal",
        "tribe", "ethnic", "hiv", "aids", "disabled", "disability", "political", "party member", "opposition",
    ];

    public static Result Parse(string? content, IReadOnlyList<InsightSource> sources, IReadOnlyList<CustomerConversationTurn> window, DateTimeOffset now)
    {
        var byTag = sources.ToDictionary(source => source.Tag, StringComparer.OrdinalIgnoreCase);
        var textByTurn = window.ToDictionary(turn => turn.TurnId, turn => new[] { turn.InboundMessageText, turn.OriginalInboundText });
        var dropped = 0;

        JsonElement root;
        try
        {
            var json = content ?? string.Empty;
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                throw new JsonException("No JSON object in the reply.");
            }

            root = JsonDocument.Parse(json[start..(end + 1)]).RootElement;
        }
        catch (JsonException)
        {
            return new Result(
                new ConversationInsights(now, sources.Count, EnoughConversation: true, null, [], [], [], null, [], null, sources), [], [], 0);
        }

        IReadOnlyList<string> ValidRefs(JsonElement element)
        {
            if (!element.TryGetProperty("refs", out var refs) || refs.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return refs.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!.Trim().Trim('[', ']'))
                .Where(tag => byTag.ContainsKey(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        InsightPoint? Point(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var text = Clean(Str(element, "text"));
            var refs = ValidRefs(element);
            if (text is null || refs.Count == 0 || IsSensitive(text))
            {
                dropped++;
                return null;
            }

            return new InsightPoint(text, refs);
        }

        IReadOnlyList<InsightPoint> Points(string name) =>
            root.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray().Select(Point).OfType<InsightPoint>().Take(6).ToArray()
                : [];

        string[] CitedText(IReadOnlyList<string> refs) =>
            refs.SelectMany(tag => textByTurn.TryGetValue(byTag[tag].TurnId, out var texts) ? texts : [])
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!)
                .ToArray();

        var facts = new List<ProspectFact>();
        if (root.TryGetProperty("facts", out var factArray) && factArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in factArray.EnumerateArray())
            {
                var field = ProspectFacts.Find(Str(item, "field"));
                var value = Clean(Str(item, "value"));
                var refs = item.ValueKind == JsonValueKind.Object ? ValidRefs(item) : [];
                if (field is null || value is null || value.Length > 200 || refs.Count == 0 || IsSensitive(value)
                    || !ProspectFacts.IsGroundedIn(value, CitedText(refs)))
                {
                    dropped++;
                    continue;
                }

                facts.RemoveAll(existing => existing.Key == field.Key);
                facts.Add(new ProspectFact(field.Key, value, ProspectFactSource.Customer, now, byTag[refs[^1]].TurnId, ConversationInsightsService.AgentKey));
            }
        }

        var interests = new List<ProspectInterest>();
        var insightInterests = new List<InsightInterest>();
        if (root.TryGetProperty("interests", out var interestArray) && interestArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in interestArray.EnumerateArray())
            {
                var named = Clean(Str(item, "product"));
                var refs = item.ValueKind == JsonValueKind.Object ? ValidRefs(item) : [];
                var strength = ParseStrength(Str(item, "strength"));
                var reason = Clean(Str(item, "text")) ?? string.Empty;
                var product = MicroinsuranceCatalog.FindByLineOrName(named)?.Name
                    ?? (named is not null && named.Length <= 60 && ProspectFacts.IsGroundedIn(named, CitedText(refs)) ? named : null);
                if (product is null || strength is null || refs.Count == 0 || IsSensitive(reason)
                    || interests.Any(existing => string.Equals(existing.Product, product, StringComparison.OrdinalIgnoreCase)))
                {
                    dropped++;
                    continue;
                }

                interests.Add(new ProspectInterest(product, strength.Value, reason, now, byTag[refs[^1]].TurnId));
                insightInterests.Add(new InsightInterest(product, strength.Value.ToString(), reason, refs));
            }
        }

        var summary = root.TryGetProperty("summary", out var summaryElement) ? Point(summaryElement) : null;
        var nextStep = root.TryGetProperty("next_step", out var nextElement) && nextElement.ValueKind == JsonValueKind.Object ? Point(nextElement) : null;
        var tone = Clean(Str(root, "tone"));
        if (tone is not null && (tone.Length > 40 || IsSensitive(tone)))
        {
            tone = null;
        }

        var insights = new ConversationInsights(
            now,
            sources.Count,
            EnoughConversation: true,
            summary,
            insightInterests,
            Points("problems"),
            Points("objections"),
            tone,
            [],
            nextStep,
            sources);
        return new Result(insights, facts, interests, dropped);
    }

    private static InterestStrength? ParseStrength(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "not_now" or "not now" => InterestStrength.NotNow,
        "asked" or "mentioned" => InterestStrength.Mentioned,
        "interested" => InterestStrength.Interested,
        "wants_it" or "wants it" => InterestStrength.WantsIt,
        _ => null,
    };

    private static bool IsSensitive(string text) =>
        SensitiveWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = DisplayText.WithoutEmDashes(string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        return cleaned.Length == 0 ? null : cleaned;
    }
}
