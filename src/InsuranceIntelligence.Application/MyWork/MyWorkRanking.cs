namespace InsuranceIntelligence.Application.MyWork;

/// <summary>
/// Deterministic, rule-based ranking over real MyWorkItemDto fields (build prompt outcome I: "My
/// Work uses the same rule and shows reasons" as the Brief, AGENTS.md non-negotiable 2: no model
/// call ever produces the score or the ranking, only narrates a result this rule already
/// produced). InsuranceIntelligence.Domain.Cases.ClaimBriefRanking's own formula reads
/// ClaimCase-only fields (CompletenessScore, ClaimedAmount, AdvisoryRiskSignals) that most My Work
/// source types (approvals, documents, agent work items) never carry, so it cannot be called
/// directly here. This rule reuses its weighting philosophy, overdue first, then due soon,
/// unassigned, escalated, approval-required, evidence gaps, priority, and how long the item has
/// sat, applied to the fields every MyWorkItemDto actually has, so both surfaces prioritize work
/// the same way.
/// </summary>
public static class MyWorkRanking
{
    private const decimal OverdueWeight = 100m;
    private const decimal DueSoonWeight = 40m;
    private const decimal EscalatedWeight = 30m;
    private const decimal ApprovalRequiredWeight = 25m;
    private const decimal EvidenceGapWeight = 20m;
    private const decimal UnassignedWeight = 15m;
    private const decimal PerWaitingDayWeight = 2m;
    private const decimal WaitingDayCap = 14m;

    public static (decimal Score, string Reason) Explain(
        MyWorkDueState dueState,
        MyWorkEvidenceReadiness evidence,
        bool approvalRequired,
        MyWorkState state,
        string priority,
        bool isAssigned,
        DateTimeOffset updatedAt,
        DateTimeOffset now)
    {
        var reasons = new List<string>();
        decimal score = 0;

        if (!isAssigned)
        {
            reasons.Add("Not yet assigned to anyone");
            score += UnassignedWeight;
        }

        switch (dueState)
        {
            case MyWorkDueState.Overdue:
                score += OverdueWeight;
                reasons.Add("Past its due date");
                break;
            case MyWorkDueState.DueSoon:
                score += DueSoonWeight;
                reasons.Add("Due within a working day");
                break;
        }

        if (state == MyWorkState.Escalated)
        {
            score += EscalatedWeight;
            reasons.Add("Escalated and needs a decision");
        }

        if (approvalRequired && state == MyWorkState.Ready)
        {
            score += ApprovalRequiredWeight;
            reasons.Add("Requires your approval");
        }

        if (evidence == MyWorkEvidenceReadiness.Gaps)
        {
            score += EvidenceGapWeight;
            reasons.Add("Evidence gaps to resolve");
        }

        score += PriorityWeight(priority);

        var waitingDays = Math.Max(0, (int)Math.Floor((now - updatedAt).TotalDays));
        if (waitingDays > 0)
        {
            score += Math.Min(waitingDays, WaitingDayCap) * PerWaitingDayWeight;
            if (waitingDays >= 3)
            {
                reasons.Add($"Waiting {waitingDays} day{(waitingDays == 1 ? "" : "s")} since it last moved");
            }
        }

        var reason = reasons.Count == 0
            ? "Open and awaiting your review"
            : string.Join(", ", reasons.Take(2)) + ".";

        return (score, reason);
    }

    private static decimal PriorityWeight(string priority) => priority.ToLowerInvariant() switch
    {
        "critical" => 18m,
        "urgent" => 15m,
        "high" => 10m,
        "medium" => 4m,
        _ => 0m,
    };
}
