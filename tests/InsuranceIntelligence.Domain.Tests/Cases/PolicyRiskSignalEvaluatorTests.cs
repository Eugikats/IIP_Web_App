using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Domain.Tests.Cases;

public sealed class PolicyRiskSignalEvaluatorTests
{
    [Fact]
    public void Evaluate_NoPolicy_ReturnsNoSignals()
    {
        var signals = PolicyRiskSignalEvaluator.Evaluate(null, new DateOnly(2026, 5, 1), 1_000_000m);

        Assert.Empty(signals);
    }

    [Fact]
    public void Evaluate_IncidentBeforeCoverStarts_FlagsNotInForce()
    {
        var policy = Policy("POL-1", effectiveFrom: new DateOnly(2026, 6, 1), effectiveTo: new DateOnly(2027, 6, 1), sumInsured: 10_000_000m);

        var signals = PolicyRiskSignalEvaluator.Evaluate(policy, new DateOnly(2026, 5, 1), null);

        Assert.Contains(PolicyRiskSignalEvaluator.PolicyNotInForceSignal, signals);
    }

    [Fact]
    public void Evaluate_IncidentAfterCoverEnds_FlagsNotInForce()
    {
        var policy = Policy("POL-1", effectiveFrom: new DateOnly(2025, 1, 1), effectiveTo: new DateOnly(2026, 1, 1), sumInsured: 10_000_000m);

        var signals = PolicyRiskSignalEvaluator.Evaluate(policy, new DateOnly(2026, 5, 1), null);

        Assert.Contains(PolicyRiskSignalEvaluator.PolicyNotInForceSignal, signals);
    }

    [Fact]
    public void Evaluate_IncidentWithinCoverPeriod_DoesNotFlagNotInForce()
    {
        var policy = Policy("POL-1", effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2027, 1, 1), sumInsured: 10_000_000m);

        var signals = PolicyRiskSignalEvaluator.Evaluate(policy, new DateOnly(2026, 5, 1), null);

        Assert.DoesNotContain(PolicyRiskSignalEvaluator.PolicyNotInForceSignal, signals);
    }

    [Fact]
    public void Evaluate_ClaimedAmountExceedsSumInsured_FlagsIt()
    {
        var policy = Policy("POL-1", effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2027, 1, 1), sumInsured: 5_000_000m);

        var signals = PolicyRiskSignalEvaluator.Evaluate(policy, new DateOnly(2026, 5, 1), claimedAmount: 8_000_000m);

        Assert.Contains(PolicyRiskSignalEvaluator.ClaimedAmountExceedsSumInsuredSignal, signals);
    }

    [Fact]
    public void Evaluate_ClaimedAmountWithinSumInsured_DoesNotFlagIt()
    {
        var policy = Policy("POL-1", effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2027, 1, 1), sumInsured: 5_000_000m);

        var signals = PolicyRiskSignalEvaluator.Evaluate(policy, new DateOnly(2026, 5, 1), claimedAmount: 3_000_000m);

        Assert.DoesNotContain(PolicyRiskSignalEvaluator.ClaimedAmountExceedsSumInsuredSignal, signals);
    }

    [Fact]
    public void Evaluate_NoSumInsuredOnFile_NeverFlagsTheAmountCheck()
    {
        var policy = Policy("POL-1", effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2027, 1, 1), sumInsured: null);

        var signals = PolicyRiskSignalEvaluator.Evaluate(policy, new DateOnly(2026, 5, 1), claimedAmount: 999_000_000m);

        Assert.DoesNotContain(PolicyRiskSignalEvaluator.ClaimedAmountExceedsSumInsuredSignal, signals);
    }

    [Fact]
    public void Evaluate_OpenEndedPolicy_NeverFlagsNotInForce()
    {
        var policy = Policy("POL-1", effectiveFrom: null, effectiveTo: null, sumInsured: 10_000_000m);

        var signals = PolicyRiskSignalEvaluator.Evaluate(policy, new DateOnly(2020, 1, 1), null);

        Assert.DoesNotContain(PolicyRiskSignalEvaluator.PolicyNotInForceSignal, signals);
    }

    private static Policy Policy(string policyNumber, DateOnly? effectiveFrom, DateOnly? effectiveTo, decimal? sumInsured) =>
        new(
            PolicyId: policyNumber,
            TenantId: "tenant-demo",
            WorkspaceId: "workspace-claims",
            PolicyNumber: policyNumber,
            ProductLine: "Motor",
            CoverType: "Comprehensive",
            CustomerId: null,
            BranchId: null,
            EffectiveFrom: effectiveFrom,
            EffectiveTo: effectiveTo,
            SumInsured: sumInsured);
}
