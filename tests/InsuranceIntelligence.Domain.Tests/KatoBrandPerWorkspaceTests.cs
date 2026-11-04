using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Whose assistant Kato is, and which covers he sells, follows the workspace being served (build-loop item 27).</summary>
public sealed class KatoBrandPerWorkspaceTests
{
    private static readonly MicroinsuranceCatalog.MicroProduct Custom = new("Mavuno Shop Cover", "Shop", "Mavuno Shop Cover pays for your stock.")
    {
        Insurer = "Zuri General Insurance",
        MonthlyPremium = 5_000m,
        SumInsured = 2_000_000m,
        Levels = [new(2_000_000m, 5_000m)],
    };

    [Fact]
    public void ConfiguredCoversReplaceTheBuiltInCatalogueEntirely()
    {
        var brand = new KatoBrand("Mavuno Bank", "Mavuno", []) { Offered = [Custom] };

        Assert.Equal([Custom], brand.Products);
        Assert.Same(Custom, brand.Find("Mavuno Shop Cover"));
        Assert.Same(Custom, brand.Find("shop"));
        Assert.Null(brand.Find("Hospital Cash"));
        Assert.Null(brand.Find(MicroinsuranceCatalog.BodaAccidentLine));
    }

    [Fact]
    public void ABrandWithNoCoversExplainsNothing()
    {
        var brand = new KatoBrand("Empty", "Empty", []) { Offered = [] };

        Assert.Empty(brand.Products);
        Assert.Empty(brand.ProductsFor(MicroinsuranceCatalog.OccupationKind.BodaRider));
        Assert.Equal(string.Empty, brand.Explain(MicroinsuranceCatalog.OccupationKind.Trader));
    }

    [Fact]
    public void TheUsualPairingsApplyWhenTheBrandSellsThoseLines_ExactlyAsTheyAlwaysWorded()
    {
        var brand = KatoBrand.NileShield;

        Assert.Equal(
            [MicroinsuranceCatalog.BodaAccident, MicroinsuranceCatalog.HospitalCash],
            brand.ProductsFor(MicroinsuranceCatalog.OccupationKind.BodaRider));
        Assert.Equal(
            $"For a boda rider, two covers fit well. The first: {MicroinsuranceCatalog.BodaAccident.Explanation} The second: {MicroinsuranceCatalog.HospitalCash.Explanation}",
            brand.Explain(MicroinsuranceCatalog.OccupationKind.BodaRider));
        Assert.Equal(
            $"For your trade, two covers fit well. The first: {MicroinsuranceCatalog.StallStock.Explanation} The second: {MicroinsuranceCatalog.HospitalCash.Explanation}",
            brand.Explain(MicroinsuranceCatalog.OccupationKind.Trader));
        Assert.Equal(
            $"One cover fits almost everyone. {MicroinsuranceCatalog.HospitalCash.Explanation}",
            brand.Explain(MicroinsuranceCatalog.OccupationKind.Other));
    }

    [Fact]
    public void ABrandWithOtherLinesOffersItsFirstTwoCovers()
    {
        var second = Custom with { Name = "Mavuno Stock Cover", ProductLine = "Stock", Explanation = "Mavuno Stock Cover pays for your goods." };
        var brand = new KatoBrand("Mavuno Bank", "Mavuno", []) { Offered = [Custom, second, Custom with { Name = "Third", ProductLine = "Third" }] };

        var picks = brand.ProductsFor(MicroinsuranceCatalog.OccupationKind.BodaRider);

        Assert.Equal([Custom, second], picks);
        Assert.Equal(
            "Two covers may fit. The first: Mavuno Shop Cover pays for your stock. The second: Mavuno Stock Cover pays for your goods.",
            brand.Explain(MicroinsuranceCatalog.OccupationKind.BodaRider));
    }

    [Fact]
    public void UseBindsABrandForTheWorkAtHandAndRestoresTheOldOneAfterwards()
    {
        var mavuno = new KatoBrand("Mavuno Bank", "Mavuno", []);
        var before = KatoBrand.Current;

        using (KatoBrand.Use(mavuno))
        {
            Assert.Equal("Mavuno", KatoBrand.Current.ShortName);
            using (KatoBrand.Use(new KatoBrand("Zuri", "Zuri", [])))
            {
                Assert.Equal("Zuri", KatoBrand.Current.ShortName);
            }

            Assert.Equal("Mavuno", KatoBrand.Current.ShortName);
        }

        Assert.Same(before, KatoBrand.Current);
    }

    [Fact]
    public async Task ABrandBoundInOneFlowIsNeverSeenByAnotherRunningAtTheSameTime()
    {
        var gate = new TaskCompletionSource();
        async Task<string> Work(string name)
        {
            using var _ = KatoBrand.Use(new KatoBrand(name, name, []));
            await gate.Task;
            await Task.Yield();
            return KatoBrand.Current.ShortName;
        }

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() => Work($"Brand-{i}"))).ToArray();
        gate.SetResult();
        var seen = await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(0, 20).Select(i => $"Brand-{i}"), seen);
        Assert.Same(KatoBrand.Default, KatoBrand.Current);
    }

    [Fact]
    public void TheOperatingSettingsCarryTheBrandAndRefuseAHalfFilledOne()
    {
        var scope = TenantScope.Create("t", "w");
        var settings = new TenantSettings { Scope = scope, AssistantOrganisation = "Mavuno Bank", AssistantShortName = "Mavuno", AssistantPartnerInsurers = "Zuri General Insurance" };

        Assert.Empty(settings.Validate());
        Assert.Contains(nameof(TenantSettings.AssistantOrganisation), settings.ChangedFrom(new TenantSettings { Scope = scope }));
        Assert.Contains(nameof(TenantSettings.AssistantPartnerInsurers), settings.ChangedFrom(new TenantSettings { Scope = scope }));
        Assert.Contains("organisation", new TenantSettings { Scope = scope, AssistantPartnerInsurers = "Zuri" }.Validate().Single());
        Assert.NotEmpty(new TenantSettings { Scope = scope, AssistantOrganisation = new string('x', 121) }.Validate());
    }
}
