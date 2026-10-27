using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Rules;

public enum DecisionOutcome
{
    /// <summary>The message went to WhatsApp.</summary>
    Sent,

    /// <summary>Recorded as it would have been sent, in a demonstration workspace with no live number or approved template. Not delivered to a phone.</summary>
    DemoSent,

    /// <summary>Considered and deliberately not sent. The reason says why.</summary>
    Skipped,

    /// <summary>Not yet: waiting for the wait period, the sending hours or the daily limit. It is looked at again.</summary>
    Scheduled,

    /// <summary>Should have gone but could not, for a reason a person can fix (a template not approved, no number connected).</summary>
    Blocked,
}

/// <summary>One check made on the way to a decision, and what it found.</summary>
public sealed record DecisionGate(string Name, bool Passed, string Detail);

/// <summary>
/// What a rule decided about one person for one event, and every check behind it. Written whether the answer was to send or
/// not, because "why did this person not get it" matters as much as "why did they". It carries no message text and no
/// details beyond what a compliance officer needs.
/// </summary>
public sealed record RuleDecision(
    string DecisionId,
    string RuleId,
    string RuleName,
    int RuleVersion,
    string EventId,
    string EventType,
    string Phone,
    string? LeadId,
    string? CustomerName,
    IReadOnlyList<DecisionGate> Gates,
    DecisionOutcome Outcome,
    string Reason,
    DateTimeOffset DecidedAt,
    DateTimeOffset? NotBefore = null,
    string? Cover = null,
    string? Insurer = null,
    string? TemplateName = null,
    string? MessageId = null,
    string Kind = "offer")
{
    public bool IsFinal => Outcome != DecisionOutcome.Scheduled;

    public bool WentOut => Outcome is DecisionOutcome.Sent or DecisionOutcome.DemoSent;
}

/// <summary>Why a conversation began, kept for the assistant: it opened with a rule's message, and it should know that when the reply comes.</summary>
public sealed record ConversationReason(
    string Phone,
    string RuleId,
    string RuleName,
    string EventType,
    string EventLabel,
    string Cover,
    string Insurer,
    string TemplateName,
    string OpeningText,
    DateTimeOffset SentAt,
    bool Demonstration);

public sealed class DecisionLog(IWorkspaceRecordStore store)
{
    public const string Kind = "decision";
    public const string ReasonKind = "reason";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task SaveAsync(TenantScope scope, RuleDecision decision, CancellationToken cancellationToken = default) =>
        store.SaveAsync(scope, Kind, decision.DecisionId, JsonSerializer.Serialize(decision, Json), cancellationToken);

    public async Task<RuleDecision?> GetAsync(TenantScope scope, string decisionId, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, Kind, decisionId, cancellationToken);
        return record is null ? null : Read<RuleDecision>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<RuleDecision>> ListAsync(
        TenantScope scope, string? ruleId = null, string? phone = null, DecisionOutcome? outcome = null, int take = 500, CancellationToken cancellationToken = default) =>
        (await store.ListAsync(scope, Kind, 5000, cancellationToken))
            .Select(record => Read<RuleDecision>(record.PayloadJson))
            .Where(decision => decision is not null)
            .Select(decision => decision!)
            .Where(decision => ruleId is null || decision.RuleId == ruleId)
            .Where(decision => phone is null || decision.Phone == phone)
            .Where(decision => outcome is null || decision.Outcome == outcome)
            .OrderByDescending(decision => decision.DecidedAt)
            .Take(Math.Clamp(take, 1, 5000))
            .ToArray();

    public Task SaveReasonAsync(TenantScope scope, ConversationReason reason, CancellationToken cancellationToken = default) =>
        store.SaveAsync(scope, ReasonKind, reason.Phone, JsonSerializer.Serialize(reason, Json), cancellationToken);

    public async Task<ConversationReason?> GetReasonAsync(TenantScope scope, string phone, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, ReasonKind, phone, cancellationToken);
        return record is null ? null : Read<ConversationReason>(record.PayloadJson);
    }

    private static T? Read<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
