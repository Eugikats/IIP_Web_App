using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Domain.Tests.Policies;

public sealed class PolicyStandingTests
{
    private static readonly DateOnly Today = new(2026, 9, 22);

    [Fact]
    public void NoDatesAtAll_IsUnknown() =>
        Assert.Equal(PolicyStandingState.Unknown, PolicyStanding.Resolve(null, null, Today));

    [Fact]
    public void StartsInTheFuture_IsNotYetStarted() =>
        Assert.Equal(PolicyStandingState.NotYetStarted, PolicyStanding.Resolve(Today.AddDays(1), Today.AddYears(1), Today));

    [Fact]
    public void EndedInThePast_IsExpired() =>
        Assert.Equal(PolicyStandingState.Expired, PolicyStanding.Resolve(Today.AddYears(-1), Today.AddDays(-1), Today));

    [Fact]
    public void WithinTheCoverWindow_IsActive() =>
        Assert.Equal(PolicyStandingState.Active, PolicyStanding.Resolve(Today.AddMonths(-1), Today.AddMonths(11), Today));

    [Fact]
    public void OnTheEffectiveToDateItself_IsStillActive() =>
        Assert.Equal(PolicyStandingState.Active, PolicyStanding.Resolve(Today.AddYears(-1), Today, Today));

    [Fact]
    public void OnlyAnEffectiveFromDate_IsActiveOnceItHasStarted() =>
        Assert.Equal(PolicyStandingState.Active, PolicyStanding.Resolve(Today.AddDays(-1), null, Today));
}
