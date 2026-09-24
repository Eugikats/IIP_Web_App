using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// Deterministic, rule-based advisory signals from real policy fields, computed once at intake
/// (build prompt outcome E: "check the policy was in force on the loss date and the amount
/// against the sum insured"). Advisory only, exactly like FraudRuleEngine and CaseReviewSignal:
/// never blocks intake, never denies or approves anything, a human reads these on the claim and
/// decides what to do. AGENTS.md non-negotiable 2: a deterministic rule over real fields, not a
/// model call, produces the signal itself.
/// </summary>
public static class PolicyRiskSignalEvaluator
{
    public const string PolicyNotInForceSignal = "Incident date falls outside the policy's cover period.";
    public const string ClaimedAmountExceedsSumInsuredSignal = "Claimed amount exceeds the policy's sum insured.";

    public static IReadOnlyList<string> Evaluate(Policy? policy, DateOnly incidentDate, decimal? claimedAmount)
    {
        if (policy is null)
        {
            return [];
        }

        var signals = new List<string>();

        if (!IsInForce(policy, incidentDate))
        {
            signals.Add(PolicyNotInForceSignal);
        }

        if (claimedAmount is { } amount && policy.SumInsured is { } sumInsured && amount > sumInsured)
        {
            signals.Add(ClaimedAmountExceedsSumInsuredSignal);
        }

        return signals;
    }

    private static bool IsInForce(Policy policy, DateOnly incidentDate)
    {
        if (policy.EffectiveFrom is { } from && incidentDate < from)
        {
            return false;
        }

        if (policy.EffectiveTo is { } to && incidentDate > to)
        {
            return false;
        }

        return true;
    }
}
