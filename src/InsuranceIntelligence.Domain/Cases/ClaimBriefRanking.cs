using InsuranceIntelligence.Domain.Scheduling;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// One ranked claim on a person's brief (build prompt outcome F): "a Domain rule ranks each
/// person's work from real fields: document completeness, working days against the product's
/// service standard, value at stake, risk signals, and how long the customer has waited." The
/// Reason is built entirely from real field values on the claim itself, never a model call, so it
/// is exactly as trustworthy as the fields it quotes.
/// </summary>
public sealed record ClaimBriefItem(
    ClaimCase ClaimCase,
    decimal Score,
    string Reason,
    string PrimaryActionLabel,
    string PrimaryActionHref);

/// <summary>
/// Deterministic, rule-based ranking over real ClaimCase fields (AGENTS.md non-negotiable 2). No
/// model is involved in the score or in which claim is ranked where; a model may only ever explain
/// a result this rule already produced, never produce the result itself. Weights are fixed
/// constants today; the build prompt names them as a future tenant setting (outcome A's "weights
/// in the priority rule are tenant settings"), not built in this pass.
/// </summary>
public static class ClaimBriefRanking
{
    private const decimal OverdueWeight = 100m;
    private const decimal DueSoonWeight = 40m;
    private const decimal PerMissingDocumentWeight = 8m;
    private const decimal PerRiskSignalWeight = 25m;
    private const decimal HighRiskLevelWeight = 20m;
    private const decimal PerCustomerWaitDayWeight = 3m;
    private const decimal CustomerWaitDayCap = 14m;

    public static IReadOnlyList<ClaimBriefItem> Rank(
        IEnumerable<ClaimCase> claims,
        IReadOnlySet<DateOnly> holidays,
        DateTimeOffset now,
        int maxItems = 5)
    {
        var scored = claims
            .Where(claimCase => claimCase.Status is not (CaseStatus.Settled or CaseStatus.Closed))
            .Select(claimCase => Score(claimCase, holidays, now))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.ClaimCase.CreatedAt)
            .ToArray();

        return scored.Take(maxItems).ToArray();
    }

    private static ClaimBriefItem Score(ClaimCase claimCase, IReadOnlySet<DateOnly> holidays, DateTimeOffset now)
    {
        var reasons = new List<string>();
        decimal score = 0;

        // Working days against the SLA the claim is actually running on (outcome B).
        if (claimCase.SlaDueAt is { } dueAt)
        {
            if (dueAt <= now)
            {
                var overdueWorkingDays = WorkingDayCalculator.CountWorkingDaysBetween(
                    DateOnly.FromDateTime(dueAt.Date), DateOnly.FromDateTime(now.Date), holidays);
                score += OverdueWeight + overdueWorkingDays * 5m;
                reasons.Add(overdueWorkingDays <= 0
                    ? "Past its service standard today"
                    : $"{overdueWorkingDays} working day{(overdueWorkingDays == 1 ? "" : "s")} past its service standard");
            }
            else if (dueAt <= now.AddHours(24))
            {
                score += DueSoonWeight;
                reasons.Add("Due within a working day");
            }
        }

        // Document completeness.
        var missingPercent = 100m - claimCase.CompletenessScore;
        if (missingPercent > 0)
        {
            var missingUnits = Math.Round(missingPercent / 10m, MidpointRounding.AwayFromZero);
            score += missingUnits * PerMissingDocumentWeight;
            reasons.Add($"{claimCase.CompletenessScore:0}% of documents ready");
        }

        // Advisory risk signals (outcome E) and the claim's own risk level.
        if (claimCase.AdvisoryRiskSignals.Count > 0)
        {
            score += claimCase.AdvisoryRiskSignals.Count * PerRiskSignalWeight;
            reasons.Add(claimCase.AdvisoryRiskSignals.Count == 1
                ? claimCase.AdvisoryRiskSignals[0]
                : $"{claimCase.AdvisoryRiskSignals.Count} advisory risk signals");
        }

        if (string.Equals(claimCase.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
        {
            score += HighRiskLevelWeight;
        }

        // Value at stake.
        if (claimCase.ClaimedAmount is { } amount && amount > 0)
        {
            score += Math.Min(amount / 1_000_000m, 50m);
            reasons.Add($"UGX {amount:N0} at stake");
        }

        // How long the customer has waited, since intake.
        var waitDays = WorkingDayCalculator.CountWorkingDaysBetween(
            DateOnly.FromDateTime(claimCase.CreatedAt.Date), DateOnly.FromDateTime(now.Date), holidays);
        if (waitDays > 0)
        {
            score += Math.Min(waitDays, CustomerWaitDayCap) * PerCustomerWaitDayWeight;
            if (waitDays >= 3)
            {
                reasons.Add($"Customer has waited {waitDays} working days");
            }
        }

        if (string.IsNullOrEmpty(claimCase.AssignedUserId))
        {
            reasons.Insert(0, "Not yet assigned to anyone");
            score += 15m;
        }

        var reason = reasons.Count == 0
            ? "Open and awaiting your review"
            : string.Join(", ", reasons.Take(2)) + ".";

        var (actionLabel, actionHref) = PrimaryAction(claimCase);

        return new ClaimBriefItem(claimCase, score, reason, actionLabel, actionHref);
    }

    private static (string Label, string Href) PrimaryAction(ClaimCase claimCase)
    {
        if (string.IsNullOrEmpty(claimCase.AssignedUserId))
        {
            return ("Assign claim", $"/app/claims/{claimCase.CaseId}");
        }

        if (claimCase.CompletenessScore < 100)
        {
            return ("Request missing documents", $"/app/claims/{claimCase.CaseId}");
        }

        if (claimCase.AdvisoryRiskSignals.Count > 0)
        {
            return ("Review risk signals", $"/app/claims/{claimCase.CaseId}");
        }

        return ("Open claim", $"/app/claims/{claimCase.CaseId}");
    }
}
