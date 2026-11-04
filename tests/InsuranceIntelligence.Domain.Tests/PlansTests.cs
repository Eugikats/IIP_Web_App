using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class PlansTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");

    [Fact]
    public void TheDefaultCatalogueHasFreeAgentTeamAndEnterpriseInThatOrderPricedInBothCurrencies()
    {
        var catalogue = PlanCatalogue.Default;

        Assert.Equal(["free", "agent", "team", "enterprise"], catalogue.Plans.Select(plan => plan.Key));
        Assert.All(catalogue.Plans.Where(plan => plan.SelfServe), plan => Assert.Equal(["UGX", "USD"], plan.Prices.Select(price => price.Currency)));
        Assert.Equal(20m, catalogue.Find("agent")!.PriceIn("USD"));
        Assert.True(catalogue.Find("free")!.IsFree);
        Assert.False(catalogue.Find("enterprise")!.SelfServe);
        Assert.Equal(PlanAllowances.Unlimited, catalogue.Find("enterprise")!.Allowances);
    }

    [Fact]
    public void ThePlansGetBiggerAsTheyGetDearer()
    {
        var plans = PlanCatalogue.Default.Plans.Where(plan => plan.SelfServe).ToArray();

        for (var i = 1; i < plans.Length; i++)
        {
            Assert.True(plans[i].Allowances.ClaimsPerMonth > plans[i - 1].Allowances.ClaimsPerMonth);
            Assert.True(plans[i].Allowances.AiTokensPerMonth > plans[i - 1].Allowances.AiTokensPerMonth);
            Assert.True(plans[i].Allowances.Seats > plans[i - 1].Allowances.Seats);
            Assert.True(plans[i].PriceIn("USD") >= plans[i - 1].PriceIn("USD"));
        }
    }

    [Fact]
    public void ADeploymentCanReplaceTheCatalogueFromConfiguration()
    {
        var catalogue = PlanCatalogue.Parse("""
            [{"key":"free","name":"Starter","rank":0,"prices":{"UGX":0},"allowances":{"claimsPerMonth":3,"seats":1}},
             {"key":"pro","name":"Pro","rank":1,"prices":{"ugx":50000,"USD":15},"allowances":{"claimsPerMonth":50}}]
            """);

        Assert.Equal(["free", "pro"], catalogue.Plans.Select(plan => plan.Key));
        Assert.Equal("Starter", catalogue.Find("free")!.Name);
        Assert.Equal(3, catalogue.Find("free")!.Allowances.ClaimsPerMonth);
        Assert.Equal(50_000m, catalogue.Find("pro")!.PriceIn("UGX"));
        Assert.Equal(0, catalogue.Find("pro")!.Allowances.Seats);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""[{"key":"pro","name":"Pro","prices":{"UGX":5}}]""")]
    [InlineData("""[{"key":"free","prices":{"UGX":5}}]""")]
    [InlineData("""[{"key":"free","prices":{"UGX":0}},{"key":"free","prices":{"UGX":0}}]""")]
    [InlineData("""[{"key":"free","prices":{"UGX":-1}}]""")]
    [InlineData("""[{"name":"no key"}]""")]
    public void AMalformedOrFreeLessCatalogueFallsBackToTheDefaults(string? json) =>
        Assert.Equal(PlanCatalogue.Default.Plans.Select(plan => plan.Key), PlanCatalogue.Parse(json).Plans.Select(plan => plan.Key));

    [Theory]
    [InlineData(0, 50, 50)]
    [InlineData(10, 50, 10)]
    [InlineData(10, 5, 5)]
    [InlineData(10, 0, 10)]
    [InlineData(0, 0, 0)]
    public void APlansLimitIsACeilingTheWorkspaceCanLowerButNeverRaise(int plan, int setting, int expected) =>
        Assert.Equal(expected, EntitlementRules.Ceiling(plan, setting));

    [Fact]
    public void AWorkspaceWithNoSubscriptionKeepsTheLimitsInItsOwnSettings()
    {
        var entitlements = EntitlementRules.For(null, PlanCatalogue.Default, seatSetting: 12, claimsSetting: 700);

        Assert.Equal(PlanCatalogue.Standard, entitlements.Plan.Key);
        Assert.Equal((12, 700, 0, 0L), (entitlements.Seats, entitlements.ClaimsPerMonth, entitlements.DocumentsPerMonth, entitlements.AiTokensPerMonth));
    }

    [Fact]
    public void AWorkspaceOnAPlanIsHeldToThePlansAllowancesAndItsOwnSettingsCanOnlyLowerThem()
    {
        var agent = new Subscription { Scope = Scope, PlanKey = "agent", StartedAt = DateTimeOffset.UtcNow, PeriodEnd = DateTimeOffset.UtcNow.AddDays(10), UpdatedAt = DateTimeOffset.UtcNow };

        var high = EntitlementRules.For(agent, PlanCatalogue.Default, seatSetting: 500, claimsSetting: 5000);
        var low = EntitlementRules.For(agent, PlanCatalogue.Default, seatSetting: 2, claimsSetting: 40);

        Assert.Equal((3, 100), (high.Seats, high.ClaimsPerMonth));
        Assert.Equal((2, 40), (low.Seats, low.ClaimsPerMonth));
        Assert.Equal(2_000_000, high.AiTokensPerMonth);
    }

    [Fact]
    public void ALapsedWorkspaceIsHeldToTheFreePlanUntilItPays()
    {
        var lapsed = new Subscription { Scope = Scope, PlanKey = "team", Status = SubscriptionStatuses.Lapsed, StartedAt = DateTimeOffset.UtcNow, PeriodEnd = DateTimeOffset.UtcNow.AddDays(-30), UpdatedAt = DateTimeOffset.UtcNow };

        var entitlements = EntitlementRules.For(lapsed, PlanCatalogue.Default, 500, 5000);

        Assert.Equal("free", entitlements.Plan.Key);
        Assert.Equal(PlanCatalogue.Default.Find("free")!.Allowances.ClaimsPerMonth, entitlements.ClaimsPerMonth);
    }

    [Fact]
    public void APastDueWorkspaceStillHasItsPlanDuringTheGrace()
    {
        var pastDue = new Subscription { Scope = Scope, PlanKey = "team", Status = SubscriptionStatuses.PastDue, StartedAt = DateTimeOffset.UtcNow, PeriodEnd = DateTimeOffset.UtcNow.AddDays(-2), UpdatedAt = DateTimeOffset.UtcNow };

        Assert.Equal("team", EntitlementRules.For(pastDue, PlanCatalogue.Default, 500, 5000).Plan.Key);
    }

    [Fact]
    public void ASubscriptionToAPlanThatNoLongerExistsFallsBackToFree()
    {
        var gone = new Subscription { Scope = Scope, PlanKey = "retired", StartedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        Assert.Equal("free", EntitlementRules.For(gone, PlanCatalogue.Default, 500, 5000).Plan.Key);
    }

    [Fact]
    public void TheNextPlanUpIsTheNextBuyableOneAndEnterpriseHasNothingAboveIt()
    {
        var catalogue = PlanCatalogue.Default;

        Assert.Equal("agent", EntitlementRules.NextUp(catalogue, catalogue.Find("free")!)!.Key);
        Assert.Equal("team", EntitlementRules.NextUp(catalogue, catalogue.Find("agent")!)!.Key);
        Assert.Null(EntitlementRules.NextUp(catalogue, catalogue.Find("team")!));
        Assert.Null(EntitlementRules.NextUp(catalogue, catalogue.Find("enterprise")!));
    }

    [Fact]
    public void AReferenceNumberIsReadableUniqueAndNamesItsMonth()
    {
        var now = new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

        var numbers = Enumerable.Range(0, 200).Select(_ => PaymentReferences.InvoiceNumber(now)).ToArray();

        Assert.All(numbers, number => Assert.Matches(@"^INV-202610-[2-9A-HJ-NP-Z]{6}$", number));
        Assert.Equal(numbers.Length, numbers.Distinct().Count());
        Assert.Matches(@"^RCT-202610-[2-9A-HJ-NP-Z]{6}$", PaymentReferences.ReceiptNumber(now));
    }

    [Fact]
    public void ThePlanLimitMessageNamesTheAllowanceAndNeverBlamesTheCustomer()
    {
        var claims = new PlanLimitExceededException("claims", "Free", 10, 10, "agent");
        var ai = new PlanLimitExceededException("ai-tokens", "Agent", 2_000_000, 2_000_001, "team");

        Assert.Contains("10 claims", claims.Message);
        Assert.Contains("Free plan", claims.Message);
        Assert.Contains("Upgrade", ai.Message);
        Assert.Equal("agent", claims.UpgradeToPlanKey);
    }
}
