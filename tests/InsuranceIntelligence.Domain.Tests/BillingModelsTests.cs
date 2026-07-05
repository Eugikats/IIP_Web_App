using InsuranceIntelligence.Domain.Billing;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class BillingModelsTests
{
    [Fact]
    public void UsageMeter_WithinLimit_NotOver_ComputesPercent()
    {
        var m = new UsageMeter("claims", "Claims", 2500, 5000, "claims", Metered: true);
        Assert.False(m.OverLimit);
        Assert.Equal(50, m.PercentUsed);
    }

    [Fact]
    public void UsageMeter_OverLimit_FlagsOver()
    {
        var m = new UsageMeter("claims", "Claims", 6000, 5000, "claims", Metered: true);
        Assert.True(m.OverLimit);
        Assert.Equal(100, m.PercentUsed);
    }

    [Fact]
    public void UsageMeter_NotMetered_NeverOverAndZeroPercent()
    {
        var m = new UsageMeter("ai-calls", "AI calls", 0, 0, "calls", Metered: false);
        Assert.False(m.OverLimit);
        Assert.Equal(0, m.PercentUsed);
    }

    [Fact]
    public void DefaultPackage_HasModulesAndSeatLimit()
    {
        Assert.NotEmpty(DefaultEntitlements.Package.Modules);
        Assert.True(DefaultEntitlements.Package.SeatLimit > 0);
    }
}
