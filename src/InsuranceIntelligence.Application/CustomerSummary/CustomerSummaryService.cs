using System.Globalization;
using System.Text;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Application.CustomerSummary;

/// <summary>One record the summary drew on. Tag is what the model cites ("S2"), Label is what a
/// person reads ("Claim CLM-726E3445"), and Href opens the record where one exists.</summary>
public sealed record CustomerSummarySource(string Tag, string Label, string Kind, string? Href);

public sealed record CustomerSummaryResult(
    string CustomerId,
    string Summary,
    IReadOnlyList<CustomerSummarySource> Sources,
    DateTimeOffset GeneratedAt,
    Guid AgentActionLogId);

public interface ICustomerSummaryService
{
    /// <summary>Null when the customer is not visible in this workspace. Throws if the model
    /// provider is unavailable; the caller turns that into an honest "could not be prepared".</summary>
    Task<CustomerSummaryResult?> SummarizeAsync(
        string customerId,
        TenantScope scope,
        string userId,
        string correlationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// The Customer Summary Agent: a short briefing on one customer for the staff member looking at
/// their Customer 360 page, built only from that customer's own records in this workspace.
///
/// Every point the model makes has to carry the tag of the record it came from, and those tags map
/// back to real policies, claims, messages and documents the page can open. The model is given no
/// knowledge base and no general context, so there is nothing for it to draw on except what the
/// insurer has actually recorded about this person. It prepares; it never decides, and it is told
/// in terms not to characterise the customer's honesty.
///
/// Contact details are deliberately withheld from the prompt. The briefing needs to know that an
/// email or phone number is on file, not what it is.
/// </summary>
public sealed class CustomerSummaryService(
    ICustomerReadRepository customerReadRepository,
    IChatCompletionProvider chatCompletionProvider,
    IAgentActionLogRepository actionLogRepository,
    IAuditRepository auditRepository,
    IAgentRunRecorder agentRunRecorder) : ICustomerSummaryService
{
    public const string AgentKey = "customer-summary";
    private const string PromptVersion = "customer-summary-v1";

    public async Task<CustomerSummaryResult?> SummarizeAsync(
        string customerId,
        TenantScope scope,
        string userId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var profile = await customerReadRepository.GetCustomerProfileAsync(scope.TenantId, scope.WorkspaceId, customerId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var sources = new List<CustomerSummarySource>();
        var lines = new List<string>();

        void Add(string kind, string label, string? href, string line)
        {
            var tag = $"S{sources.Count + 1}";
            sources.Add(new CustomerSummarySource(tag, label, kind, href));
            lines.Add($"[{tag}] {line}");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var policy in profile.Policies)
        {
            var number = Value(policy, "policyNumber") ?? "Unnumbered policy";
            var from = ParseDate(Value(policy, "effectiveFrom"));
            var to = ParseDate(Value(policy, "effectiveTo"));
            var standing = to is { } end && end < today ? "expired"
                : from is { } start && start > today ? "not yet started"
                : to is not null || from is not null ? "in force" : "dates not recorded";
            Add("policy", $"Policy {number}", null,
                $"Policy {number}: {Value(policy, "productLine") ?? "product not recorded"}, {Value(policy, "coverType") ?? "cover type not recorded"}, "
                + $"effective {Describe(from)} to {Describe(to)} ({standing}).");
        }

        foreach (var claim in profile.Claims)
        {
            var reference = Guid.TryParse(Value(claim, "caseId"), out var caseId) ? ClaimReference.For(caseId) : "a claim";
            var href = caseId == Guid.Empty ? null : $"/app/claims/{caseId}";
            Add("claim", $"Claim {reference}", href,
                $"Claim {reference}: {Value(claim, "productLine")} {Value(claim, "claimType")}, status {ClaimStatusLabels.For(Value(claim, "status"))}, "
                + $"incident {Describe(ParseDate(Value(claim, "incidentDate")))}, reported {Describe(ParseDate(Value(claim, "createdAt")))} "
                + $"via {Value(claim, "channel") ?? "an unrecorded channel"}, documents {Percent(Value(claim, "completenessScore"))} complete, "
                + $"risk level {Value(claim, "riskLevel") ?? "not recorded"}, on policy {Value(claim, "policyNumber") ?? "not recorded"}.");
        }

        foreach (var message in profile.Interactions.OrderByDescending(item => Value(item, "createdAt")).Take(8))
        {
            var reference = Guid.TryParse(Value(message, "caseId"), out var caseId) && caseId != Guid.Empty ? ClaimReference.For(caseId) : null;
            var when = Describe(ParseDate(Value(message, "createdAt")));
            Add("message", $"{Value(message, "channel") ?? "Message"} on {when}", reference is null ? null : $"/app/claims/{caseId}",
                $"{Value(message, "direction") ?? "Message"} {Value(message, "channel")} message on {when}"
                + (reference is null ? string.Empty : $" about {reference}")
                + $", delivery {Value(message, "deliveryStatus") ?? "not recorded"}: \"{Value(message, "messageSnippet")}\".");
        }

        foreach (var document in profile.Documents.Take(12))
        {
            var reference = Guid.TryParse(Value(document, "caseId"), out var caseId) && caseId != Guid.Empty ? ClaimReference.For(caseId) : null;
            var fileName = Value(document, "fileName") ?? "document";
            Add("document", fileName, reference is null ? null : $"/app/claims/{caseId}",
                $"Document {fileName} ({Humanize(Value(document, "documentType"))})"
                + (reference is null ? string.Empty : $" on {reference}")
                + $", uploaded {Describe(ParseDate(Value(document, "uploadedAt")))}, processing {Humanize(Value(document, "processingStatus"))}.");
        }

        Add("consent", "Consent record", null, profile.Consent.Count == 0
            ? "No consent record is on file for this customer."
            : "Consent on file: " + string.Join("; ", profile.Consent.Select(entry => $"{Humanize(entry.Key)} {entry.Value}")) + ".");

        var identity = profile.Identity;
        var name = Value(identity, "fullName") ?? "This customer";
        var prompt = new StringBuilder()
            .AppendLine("You prepare a short briefing about one insurance customer for staff at a Ugandan insurer.")
            .AppendLine("Use ONLY the records listed below. Never state anything that is not in them, and do not guess at anything that is not recorded.")
            .AppendLine("Do not approve, reject, settle or recommend a decision on any claim, and never suggest the customer is dishonest or fraudulent.")
            .AppendLine("Refer to claims by their reference (for example CLM-1A2B3C4D) and to policies by their policy number. Never print internal identifiers.")
            .AppendLine("Write plain English for a busy claims officer. Do not use em dashes.")
            .AppendLine(CultureInfo.InvariantCulture, $"Today's date is {today:d MMMM yyyy}.")
            .AppendLine()
            .AppendLine("Reply in exactly this format and nothing else:")
            .AppendLine("**Overview**")
            .AppendLine("- Two to four short bullet points: what this customer holds with the insurer and where their claims stand. End every bullet with the tags it relies on, like [S1] or [S1][S2].")
            .AppendLine("**Needs attention**")
            .AppendLine("- Bullet points for anything staff should act on or check, for example a claim reported after its policy ended, a claim waiting on documents, a message that has not been answered, or no consent on file. End every bullet with its tags. If nothing needs attention, write exactly one bullet: - Nothing in these records needs attention.")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"CUSTOMER: {name}")
            .AppendLine(CultureInfo.InvariantCulture, $"Email on file: {(Value(identity, "email") is null ? "no" : "yes")}. Phone on file: {(Value(identity, "phone") is null ? "no" : "yes")}.")
            .AppendLine()
            .AppendLine("RECORDS:");
        foreach (var line in lines)
        {
            prompt.AppendLine(line);
        }

        ChatCompletionResult chatResult;
        try
        {
            chatResult = await chatCompletionProvider.CompleteAsync(
                new ChatCompletionRequest(prompt.ToString(), scope.TenantId, scope.WorkspaceId, correlationId, AgentKey),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await agentRunRecorder.RecordAsync(
                scope, AgentKey, "customer_summary_request", [$"customer:{profile.CustomerId}"],
                AgentRunStatus.Failed, startedAt, correlationId, userId, caseId: null, errorCategory: "provider_unavailable",
                cancellationToken: cancellationToken);
            throw;
        }

        var summary = DisplayText.WithoutEmDashes(chatResult.Content).Trim();
        var generatedAt = DateTimeOffset.UtcNow;

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = null,
            AgentName = AgentKey,
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = $"Customer summary for {name}",
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = summary,
            ConfidenceScore = AiConfidenceScore.Create(profile.Claims.Count + profile.Policies.Count > 0 ? 0.74m : 0.5m),
            RiskFlag = "needs_review",
            HumanOverride = false,
            CreatedAt = generatedAt,
            CorrelationId = correlationId,
            UserId = userId,
            ModelProvider = chatResult.Provider,
            ModelDeployment = chatResult.Deployment,
            PromptVersion = PromptVersion,
            Visibility = "Internal",
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);
        await agentRunRecorder.RecordAsync(
            scope, AgentKey, "customer_summary_request", [$"customer:{profile.CustomerId}"],
            AgentRunStatus.Completed, startedAt, correlationId, userId, caseId: null, cancellationToken: cancellationToken);

        return new CustomerSummaryResult(profile.CustomerId, summary, sources, generatedAt, actionLog.AgentActionLogId);
    }

    private static string? Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant)
            ? DateOnly.FromDateTime(instant.UtcDateTime)
            : null;
    }

    private static string Describe(DateOnly? date) =>
        date is { } value ? value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "an unrecorded date";

    private static string Percent(string? value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return "an unrecorded share";
        }

        var pct = number is > 0 and <= 1 ? number * 100 : number;
        return $"{Math.Round(pct)}%";
    }

    private static string Humanize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "not recorded";
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character is '_' or '-')
            {
                builder.Append(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(character) && char.IsLower(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
