using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Voice;

/// <summary>The services a call needs, made fresh for one use, as the person who opened the call.</summary>
public interface IVoiceScope : IDisposable
{
    ICommandCenterConversationService Conversations { get; }

    IAgentBehaviourProfileRepository Profiles { get; }

    IAuditRepository Audit { get; }
}

public interface IVoiceScopeFactory
{
    /// <summary>A scope whose tenant context is the given person's, so every read is theirs and nobody else's.</summary>
    IVoiceScope Create(TenantContext context);
}

public sealed record VoiceBlocksEvent(IReadOnlyList<CommandCenterResponseBlock> Blocks);

public sealed record VoiceOpenEvent(string Label, string Href);

public sealed record VoiceConversationEvent(Guid ConversationId);

/// <param name="OutputJson">What the model is told. Plain words and numbers from a tool result, never an exception.</param>
/// <param name="Outcome">ok, locked, refused, invalid, failed. Recorded in the audit trail with the tool name, never the content.</param>
public sealed record VoiceToolResult(string OutputJson, string Outcome, bool EndsCall = false);

/// <summary>
/// What the voice agent can do. Every tool is read, show or prepare: nothing here sends, approves, pays or
/// changes a status, and nothing the model says can add a tool. While the phone is locked every tool except
/// ending the call answers "locked", so no figures are read aloud to whoever is holding the phone.
/// </summary>
public sealed partial class VoiceToolbox(IVoiceScopeFactory scopes)
{
    public const int MaxToolCallsPerSession = 60;
    private const int MaxQuestionLength = 500;
    private const int MaxSpokenCharacters = 1500;

    public const string EndConversation = "end_conversation";

    public static JsonArray Definitions() =>
    [
        Tool("ask_analyst",
            "Ask the workspace analyst a question about claims, prospects, approvals, documents or any figure. Use it for every fact about the workspace. Never state a figure that did not come from this tool.",
            Params(("question", "The person's own question, in their own words as closely as you can, with the date range if they gave one.")), "question"),
        Tool("make_visual",
            "Show the person a chart or table for a question. The visual appears on their screen; say one sentence about what it shows.",
            Params(("question", "What to chart or tabulate, for example claims received each day this week.")), "question"),
        Tool("show_on_screen",
            "Offer a button on the person's screen to open something an earlier answer mentioned. It only opens things the analyst already returned.",
            Params(("label", "Part of the name of the claim, queue or document as it was mentioned.")), "label"),
        Tool("prepare_draft",
            "Prepare a draft customer update for a claim and save it for a person to review and approve. Nothing is ever sent from here.",
            Params(("claim_reference", "The claim reference, for example CLM-1234.")), "claim_reference"),
        Tool(EndConversation, "End the conversation when the person says they are done.", new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }),
    ];

    public async Task<VoiceToolResult> ExecuteAsync(VoiceSession session, string name, string? argumentsJson, CancellationToken cancellationToken)
    {
        if (name == EndConversation)
        {
            session.EndRequested = true;
            return new VoiceToolResult("{\"ended\":true}", "ok", EndsCall: true);
        }

        if (session.Locked)
        {
            return new VoiceToolResult(
                Json(new { locked = true, message = "The phone is locked, so no figures can be read out. Tell the person to unlock the phone and ask again." }),
                "locked");
        }

        if (++session.ToolCalls > MaxToolCallsPerSession)
        {
            return new VoiceToolResult(Json(new { error = "too_many_requests", message = "That is a lot of questions for one call. Tell the person to start a new conversation." }), "refused");
        }

        JsonElement arguments;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Invalid("The request could not be read.");
        }

        try
        {
            switch (name)
            {
                case "ask_analyst":
                    return TryText(arguments, "question", MaxQuestionLength, out var question)
                        ? await AskAsync(session, question, showEverything: false, cancellationToken)
                        : Invalid("Ask with a question of up to 500 characters.");
                case "make_visual":
                    return TryText(arguments, "question", MaxQuestionLength, out var visualQuestion)
                        ? await AskAsync(session, visualQuestion, showEverything: true, cancellationToken)
                        : Invalid("Say what to chart or tabulate, in up to 500 characters.");
                case "show_on_screen":
                    return TryText(arguments, "label", 120, out var label) ? ShowOnScreen(session, label) : Invalid("Say what to open.");
                case "prepare_draft":
                    return TryText(arguments, "claim_reference", 40, out var reference) && ClaimReferencePattern().IsMatch(reference)
                        ? await PrepareDraftAsync(session, reference, cancellationToken)
                        : Invalid("A claim reference, such as CLM-1234, is needed.");
                default:
                    return new VoiceToolResult(Json(new { error = "unknown_tool", message = "That is not something I can do." }), "refused");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AiCreditsExhaustedException)
        {
            return new VoiceToolResult(Json(new { error = "credits_used_up", message = "This month's AI credits are used up. Questions that only count records still work in the written chat. Tell the person an administrator can add credits." }), "refused");
        }
        catch (PermissionDeniedException)
        {
            return new VoiceToolResult(Json(new { error = "not_allowed", message = "The person does not have permission to see that." }), "refused");
        }
        catch (CommandCenterValidationException exception)
        {
            return Invalid(DisplayText.WithoutEmDashes(exception.Message));
        }
        catch (Exception)
        {
            // The reason stays out of the model's hands: it would read an exception message aloud.
            return new VoiceToolResult(Json(new { error = "failed", message = "I could not get that just now. Tell the person to try again, or type it." }), "failed");
        }
    }

    private async Task<VoiceToolResult> AskAsync(VoiceSession session, string question, bool showEverything, CancellationToken cancellationToken)
    {
        using var scope = scopes.Create(session.Context);
        var response = await scope.Conversations.QueryAsync(
            new CommandCenterQueryRequest(question, session.ConversationId, Guid.NewGuid(), session.TimeZoneOffsetMinutes),
            $"voice-{session.Id}", cancellationToken);
        Follow(session, response);
        session.RememberLinks(LinksFrom(response.Blocks));

        var shown = showEverything ? response.Blocks : response.Blocks.Where(block => block is not TextBlock).ToList();
        var onScreen = shown.Count > 0;
        if (onScreen)
        {
            session.Events.Writer.TryWrite(new VoiceScreenEvent("blocks", new VoiceBlocksEvent(shown)));
        }

        return new VoiceToolResult(Json(new { answer = Summarise(response.Blocks), on_screen = onScreen }), "ok");
    }

    /// <summary>The call adds to one written conversation. When the first question starts it, the page is told which, so typing continues the same chat.</summary>
    private static void Follow(VoiceSession session, CommandCenterQueryResponse response)
    {
        var started = session.ConversationId is null && response.ConversationId is not null;
        session.ConversationId = response.ConversationId ?? session.ConversationId;
        if (started)
        {
            session.Events.Writer.TryWrite(new VoiceScreenEvent("conversation", new VoiceConversationEvent(response.ConversationId!.Value)));
        }
    }

    private static VoiceToolResult ShowOnScreen(VoiceSession session, string label)
    {
        var link = session.FindLink(label);
        if (link is null)
        {
            return new VoiceToolResult(Json(new { shown = false, message = "There is nothing to open for that yet. Ask about it first." }), "ok");
        }

        session.Events.Writer.TryWrite(new VoiceScreenEvent("open", new VoiceOpenEvent(link.Label, link.Href)));
        return new VoiceToolResult(Json(new { shown = true, message = $"A button to open {link.Label} is on the person's screen." }), "ok");
    }

    private async Task<VoiceToolResult> PrepareDraftAsync(VoiceSession session, string reference, CancellationToken cancellationToken)
    {
        using var scope = scopes.Create(session.Context);
        var response = await scope.Conversations.QueryAsync(
            new CommandCenterQueryRequest($"Draft an update for the customer on claim {reference}", session.ConversationId, Guid.NewGuid(), session.TimeZoneOffsetMinutes),
            $"voice-{session.Id}", cancellationToken);
        Follow(session, response);

        if (response.Blocks.Any(block => block is DraftCommunicationBlock))
        {
            session.Events.Writer.TryWrite(new VoiceScreenEvent("blocks", new VoiceBlocksEvent(response.Blocks)));
            return new VoiceToolResult(
                Json(new { prepared = true, status = "saved for approval", message = "A draft customer update is saved for a person to review and approve. Nothing has been sent." }), "ok");
        }

        return new VoiceToolResult(Json(new { prepared = false, message = Summarise(response.Blocks) }), "ok");
    }

    /// <summary>What the model may say about an answer: its words and its figures, and a note where something is on screen.</summary>
    public static string Summarise(IReadOnlyList<CommandCenterResponseBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case TextBlock text: parts.Add(Plain(text.Markdown)); break;
                case MetricPillsBlock pills: parts.Add(string.Join("; ", pills.Items.Select(item => $"{item.Label}: {item.Value}"))); break;
                case ChartBlock chart:
                    parts.Add($"Chart, {chart.Title}. {chart.Summary}".Trim());
                    if (chart.Points.Count is > 0 and <= 8)
                    {
                        parts.Add(string.Join("; ", chart.Points.Select(point => $"{point.Label} {point.Value:0.##}")));
                    }
                    break;
                case ClaimsTableBlock table:
                    parts.Add($"{table.Rows.Count} claims are listed on the screen." + (table.Rows.Count > 0 ? " The first are " + string.Join("; ", table.Rows.Take(3).Select(row => $"{row.Customer}, {row.Status}")) + "." : string.Empty));
                    break;
                case SlaSummaryBlock sla: parts.Add($"{sla.Items.Count} claims with service deadlines are on the screen."); break;
                case DocumentGapSummaryBlock gaps: parts.Add($"{gaps.Items.Count} claims with missing documents are on the screen."); break;
                case ApprovalQueueBlock approvals: parts.Add($"{approvals.Items.Count} approvals are waiting for a person, shown on the screen."); break;
                case DraftCommunicationBlock draft: parts.Add("A draft is saved for approval and nothing has been sent. " + Cap(Plain(draft.Preview), 300)); break;
                case ManagementSummaryBlock summary: parts.Add($"{summary.Title}: " + string.Join("; ", summary.Sections.Select(section => section.Heading))); break;
                case ActionProposalBlock: parts.Add("Suggested next steps are on the screen. Each needs a person to decide."); break;
                case NotConfiguredBlock notConfigured: parts.Add($"{notConfigured.Provider} is not set up. {notConfigured.AdminAction}"); break;
                case ErrorBlock error: parts.Add(error.SafeMessage); break;
                case CreditsNoticeBlock credits: parts.Add(credits.Message); break;
            }
        }

        return Cap(DisplayText.WithoutEmDashes(string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)))), MaxSpokenCharacters);
    }

    private static IEnumerable<VoiceLink> LinksFrom(IReadOnlyList<CommandCenterResponseBlock> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case LinkBlock link: yield return new VoiceLink(link.Label, link.Href); break;
                case ClaimsTableBlock table:
                    foreach (var row in table.Rows.Take(20))
                    {
                        yield return new VoiceLink($"{row.Customer} {row.CaseId}", row.Href);
                    }
                    break;
                case ApprovalQueueBlock approvals:
                    foreach (var item in approvals.Items.Take(20))
                    {
                        yield return new VoiceLink(item.Title, item.Href);
                    }
                    break;
                case DraftCommunicationBlock draft: yield return new VoiceLink("the draft customer update", $"/customer-updates/{draft.DraftId}"); break;
            }
        }
    }

    private static string Plain(string markdown) =>
        Regex.Replace(markdown, @"[*_`#>]+", string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

    private static string Cap(string text, int max) => text.Length <= max ? text : text[..max].TrimEnd() + "...";

    private static bool TryText(JsonElement arguments, string name, int maxLength, out string value)
    {
        value = string.Empty;
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString()?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > maxLength)
        {
            return false;
        }

        value = text;
        return true;
    }

    private static VoiceToolResult Invalid(string message) => new(Json(new { error = "invalid", message }), "invalid");

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static JsonObject Tool(string name, string description, JsonObject parameters, params string[] required)
    {
        if (required.Length > 0)
        {
            parameters["required"] = new JsonArray(required.Select(item => (JsonNode)item).ToArray());
        }

        return new JsonObject { ["type"] = "function", ["name"] = name, ["description"] = description, ["parameters"] = parameters };
    }

    private static JsonObject Params(params (string Name, string Description)[] properties)
    {
        var bag = new JsonObject();
        foreach (var (name, description) in properties)
        {
            bag[name] = new JsonObject { ["type"] = "string", ["description"] = description };
        }

        return new JsonObject { ["type"] = "object", ["properties"] = bag };
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9\-_/]{1,39}$")]
    private static partial Regex ClaimReferencePattern();
}
