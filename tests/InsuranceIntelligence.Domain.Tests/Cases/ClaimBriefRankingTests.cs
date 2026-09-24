using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests.Cases;

public sealed class ClaimBriefRankingTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rank_ReturnsAtMostFiveItems()
    {
        var claims = Enumerable.Range(0, 10).Select(i => Claim($"claim-{i}", overdue: true)).ToArray();

        var ranked = ClaimBriefRanking.Rank(claims, NoHolidays, Now);

        Assert.Equal(5, ranked.Count);
    }

    [Fact]
    public void Rank_ExcludesSettledAndClosedClaims()
    {
        var open = Claim("open", overdue: true);
        var settled = open with { CaseId = Guid.NewGuid(), Status = CaseStatus.Settled };
        var closed = open with { CaseId = Guid.NewGuid(), Status = CaseStatus.Closed };

        var ranked = ClaimBriefRanking.Rank([open, settled, closed], NoHolidays, Now);

        Assert.Single(ranked);
        Assert.Equal(open.CaseId, ranked[0].ClaimCase.CaseId);
    }

    [Fact]
    public void Rank_OverdueClaimOutranksAnOnTrackClaim()
    {
        var overdue = Claim("overdue", overdue: true);
        var onTrack = Claim("on-track", overdue: false) with { SlaDueAt = Now.AddDays(5) };

        var ranked = ClaimBriefRanking.Rank([onTrack, overdue], NoHolidays, Now);

        Assert.Equal(overdue.CaseId, ranked[0].ClaimCase.CaseId);
    }

    [Fact]
    public void Rank_UnassignedClaimIsFlaggedInTheReason()
    {
        var unassigned = Claim("unassigned", overdue: false) with { AssignedUserId = null };

        var ranked = ClaimBriefRanking.Rank([unassigned], NoHolidays, Now);

        Assert.Contains("not yet assigned", ranked[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Assign claim", ranked[0].PrimaryActionLabel);
    }

    [Fact]
    public void Rank_LowCompletenessAppearsInTheReason()
    {
        var claim = Claim("incomplete", overdue: false) with { CompletenessScore = 40m };

        var ranked = ClaimBriefRanking.Rank([claim], NoHolidays, Now);

        Assert.Contains("40%", ranked[0].Reason);
    }

    [Fact]
    public void Rank_AdvisoryRiskSignalAppearsVerbatimInTheReason()
    {
        var claim = Claim("risky", overdue: false) with
        {
            CompletenessScore = 100m,
            AdvisoryRiskSignals = [PolicyRiskSignalEvaluator.PolicyNotInForceSignal],
        };

        var ranked = ClaimBriefRanking.Rank([claim], NoHolidays, Now);

        Assert.Contains(PolicyRiskSignalEvaluator.PolicyNotInForceSignal, ranked[0].Reason);
        Assert.Equal("Review risk signals", ranked[0].PrimaryActionLabel);
    }

    [Fact]
    public void Rank_ClaimedAmountAppearsInTheReason()
    {
        var claim = Claim("high-value", overdue: false) with { CompletenessScore = 100m, ClaimedAmount = 12_000_000m };

        var ranked = ClaimBriefRanking.Rank([claim], NoHolidays, Now);

        Assert.Contains("12,000,000", ranked[0].Reason);
    }

    [Fact]
    public void Rank_HigherRiskSignalCountOutranksFewerSignals()
    {
        var lowRisk = Claim("low-risk", overdue: false) with { CompletenessScore = 100m, AdvisoryRiskSignals = [PolicyRiskSignalEvaluator.PolicyNotInForceSignal] };
        var highRisk = Claim("high-risk", overdue: false) with
        {
            CompletenessScore = 100m,
            AdvisoryRiskSignals = [PolicyRiskSignalEvaluator.PolicyNotInForceSignal, PolicyRiskSignalEvaluator.ClaimedAmountExceedsSumInsuredSignal],
        };

        var ranked = ClaimBriefRanking.Rank([lowRisk, highRisk], NoHolidays, Now);

        Assert.Equal(highRisk.CaseId, ranked[0].ClaimCase.CaseId);
    }

    private static ClaimCase Claim(string suffix, bool overdue) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = $"Customer {suffix}",
            PolicyNumber = $"POL-{suffix}",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 9, 1),
            IncidentDescription = "Test claim.",
            Status = CaseStatus.Assigned,
            CompletenessScore = 100m,
            AssignedUserId = "officer-1",
            SlaDueAt = overdue ? Now.AddDays(-1) : null,
            CreatedByUserId = "officer-1",
            CreatedAt = Now.AddDays(-1),
            UpdatedAt = Now.AddDays(-1),
            CorrelationId = $"corr-{suffix}",
        };
}
