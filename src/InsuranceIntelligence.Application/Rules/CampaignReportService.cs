using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Rules;

public sealed record SkipReason(string Reason, int People);

/// <summary>One rule's results, from first look to conversion. Nothing here is estimated: every number is a count of records.</summary>
public sealed record RuleResults(
    string RuleId,
    string RuleName,
    int Considered,
    int Sent,
    int ShownAsSent,
    int Delivered,
    int Read,
    int Answered,
    int AskedForDetails,
    int ConvertedAfterOffer,
    int Waiting,
    int CouldNotSend,
    int NotSent,
    IReadOnlyList<SkipReason> WhyNotSent);

/// <summary>
/// Reporting for the rules: how many people each rule looked at, how many it messaged, how many of those messages WhatsApp says were
/// delivered and read, who answered, who went on to a cover, and for everyone it did not message, why not. Delivered and read are
/// counted from WhatsApp's own reports for real sends only: a message shown as sent in a demonstration was never delivered and is
/// never counted as if it was.
/// </summary>
public sealed class CampaignReportService(
    RuleService rules,
    DecisionLog log,
    OutcomeService outcomes,
    ConversionReportService conversions,
    IWhatsAppMessageRepository messages)
{
    public async Task<IReadOnlyList<RuleResults>> BuildAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var ruleList = await rules.ListAsync(scope, cancellationToken);
        var decisions = await log.ListAsync(scope, take: 5000, cancellationToken: cancellationToken);
        var replies = await outcomes.ListAsync(scope, take: 5000, cancellationToken: cancellationToken);
        var converted = (await conversions.BuildAsync(scope, cancellationToken)).Converted;
        var results = new List<RuleResults>();

        foreach (var rule in ruleList)
        {
            var mine = decisions.Where(decision => decision.RuleId == rule.RuleId && decision.Kind != "follow_up").ToArray();
            var delivered = 0;
            var read = 0;
            foreach (var sent in mine.Where(decision => decision.Outcome == DecisionOutcome.Sent && Guid.TryParse(decision.MessageId, out _)))
            {
                var message = await messages.GetAsync(scope, Guid.Parse(sent.MessageId!), cancellationToken);
                if (message?.DeliveryState == WhatsAppDeliveryState.Read)
                {
                    delivered++;
                    read++;
                }
                else if (message?.DeliveryState == WhatsAppDeliveryState.Delivered)
                {
                    delivered++;
                }
            }

            var theirReplies = replies.Where(reply => reply.RuleId == rule.RuleId).ToArray();
            var why = mine.Where(decision => decision.Outcome == DecisionOutcome.Skipped)
                .GroupBy(decision => decision.Reason)
                .Select(group => new SkipReason(group.Key, group.Count()))
                .OrderByDescending(item => item.People)
                .Take(5)
                .ToArray();
            results.Add(new RuleResults(
                rule.RuleId, rule.Name,
                mine.Length,
                mine.Count(decision => decision.Outcome == DecisionOutcome.Sent),
                mine.Count(decision => decision.Outcome == DecisionOutcome.DemoSent),
                delivered,
                read,
                theirReplies.Length,
                theirReplies.Count(reply => reply.Kind is OutcomeKind.Interested or OutcomeKind.RenewalRequested),
                converted.Count(person => person.RuleId == rule.RuleId),
                mine.Count(decision => decision.Outcome == DecisionOutcome.Scheduled),
                mine.Count(decision => decision.Outcome == DecisionOutcome.Blocked),
                mine.Count(decision => decision.Outcome == DecisionOutcome.Skipped),
                why));
        }

        return results.Where(result => result.Considered > 0).OrderByDescending(result => result.Considered).ToArray();
    }
}
