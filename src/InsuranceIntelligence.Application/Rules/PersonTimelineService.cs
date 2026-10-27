using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Rules;

/// <summary>One step in a person's story: something the bank told us, something a rule decided, what was sent, how they answered.</summary>
public sealed record TimelineEntry(
    DateTimeOffset At,
    string Kind,
    string Title,
    string Detail,
    IReadOnlyList<DecisionGate>? Gates = null,
    string? Outcome = null);

public sealed record PersonTimeline(string Phone, string? CustomerName, string? ConsentSummary, IReadOnlyList<TimelineEntry> Entries);

/// <summary>
/// "Why did this person get this message?" in one place, oldest first: the agreement they gave, what happened at the bank, each
/// rule's decision with every check it made, the words that went out, and what they said back. It answers a compliance officer's
/// first question and is what a rehearsal shows. It reads only this workspace's records.
/// </summary>
public sealed class PersonTimelineService(EventService events, DecisionLog log, OutcomeService outcomes, ISalesLeadWriteRepository leads)
{
    public async Task<PersonTimeline> GetAsync(TenantScope scope, string phone, CancellationToken cancellationToken = default)
    {
        var entries = new List<TimelineEntry>();
        var lead = await leads.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, phone, cancellationToken);

        string? consentSummary = null;
        if (lead?.Consent is { } consent)
        {
            consentSummary = $"{consent.Status} on {consent.At:d MMM yyyy}: {consent.Wording}";
            entries.Add(new TimelineEntry(consent.At, "consent", consent.Status switch
            {
                Domain.Acquisition.ContactConsentStatus.Agreed => "Agreed to be contacted",
                Domain.Acquisition.ContactConsentStatus.Declined => "Said no to being contacted",
                Domain.Acquisition.ContactConsentStatus.Withdrawn => "Withdrew the agreement to be contacted",
                _ => "Consent recorded",
            }, $"{consent.Wording} (source: {consent.Channel}).", Outcome: consent.Status.ToString()));
        }

        foreach (var item in (await events.ListAsync(scope, take: 1000, cancellationToken: cancellationToken)).Where(item => item.Phone == phone))
        {
            var type = BankEventCatalog.Find(item.EventType);
            var details = string.Join(", ", item.Details
                .Where(pair => pair.Key is "amount" or "term_months" or "branch" or "product" or "end_date" or "due_date" or "claim_reference")
                .Select(pair => pair.Key == "amount" && decimal.TryParse(pair.Value, out var amount) ? $"UGX {amount:N0}" : pair.Key == "term_months" ? $"{pair.Value} months" : pair.Value));
            entries.Add(new TimelineEntry(item.OccurredAt, "event", type?.Label ?? item.EventType,
                (details.Length > 0 ? details + ". " : string.Empty) + $"Came from: {(item.Source == EventSource.PolicyDates ? "dates in the records" : item.Source == EventSource.FileImport ? "a file" : "entered by hand")}."));
        }

        foreach (var decision in await log.ListAsync(scope, phone: phone, take: 500, cancellationToken: cancellationToken))
        {
            entries.Add(new TimelineEntry(decision.DecidedAt, "decision", $"{decision.RuleName}: {Label(decision.Outcome)}", decision.Reason, decision.Gates, decision.Outcome.ToString()));
        }

        var reasonByRule = new Dictionary<string, ConversationReason>();
        if (await log.GetReasonAsync(scope, phone, cancellationToken) is { } reason)
        {
            reasonByRule[reason.RuleId] = reason;
            entries.Add(new TimelineEntry(reason.SentAt, "message", reason.Demonstration ? "What went out (shown as sent, not delivered)" : "What went out", reason.OpeningText));
        }

        foreach (var outcome in (await outcomes.ListAsync(scope, take: 1000, cancellationToken: cancellationToken)).Where(item => item.Phone == phone))
        {
            entries.Add(new TimelineEntry(outcome.At, "reply", outcome.Kind switch
            {
                OutcomeKind.Interested => "Asked for details",
                OutcomeKind.NotNow => "Said not now",
                OutcomeKind.Declined => "Said no thanks",
                OutcomeKind.Stopped => "Said stop",
                OutcomeKind.RenewalRequested => "Asked to renew",
                OutcomeKind.Applied => "Took the cover",
                _ => "Replied",
            }, outcome.Reply is null ? "No words recorded." : $"They wrote: {outcome.Reply}", Outcome: outcome.Kind.ToString()));
        }

        return new PersonTimeline(phone, lead?.CustomerName, consentSummary, entries.OrderBy(entry => entry.At).ThenBy(entry => entry.Kind, StringComparer.Ordinal).ToArray());
    }

    private static string Label(DecisionOutcome outcome) => outcome switch
    {
        DecisionOutcome.Sent => "sent",
        DecisionOutcome.DemoSent => "shown as sent",
        DecisionOutcome.Skipped => "not sent",
        DecisionOutcome.Scheduled => "waiting",
        _ => "could not send",
    };
}
