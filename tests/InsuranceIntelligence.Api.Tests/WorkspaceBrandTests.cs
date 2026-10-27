using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Whose assistant Kato is belongs to the workspace, not to the process, and one workspace's records never
/// show in another.</summary>
public sealed class WorkspaceBrandTests
{
    private static readonly TenantScope Bank = new("org-bank", "main");
    private static readonly TenantScope Insurer = new("org-insurer", "main");

    private static InsuranceIntelligenceDbContext Db() =>
        new(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static WorkspaceBrandSettings MavunoBank() => new(
        "Mavuno Bank",
        "Mavuno",
        [MicroinsuranceCatalog.Mpingo, MicroinsuranceCatalog.Kifaru],
        LineFocus.Both);

    [Fact]
    public async Task ARecordIsOnlyVisibleToItsOwnWorkspace()
    {
        await using var db = Db();
        var store = new EfWorkspaceRecordStore(db);
        await store.SaveAsync(Bank, "brand", "current", "{\"a\":1}");

        Assert.NotNull(await store.GetAsync(Bank, "brand", "current"));
        Assert.Null(await store.GetAsync(Insurer, "brand", "current"));
        Assert.Single(await store.ListAsync(Bank, "brand"));
        Assert.Empty(await store.ListAsync(Insurer, "brand"));
        Assert.Equal(1, await store.CountAsync(Bank, "brand"));
        Assert.Equal(0, await store.CountAsync(Insurer, "brand"));
    }

    [Fact]
    public async Task TheSameKeyInTwoWorkspacesIsTwoRecords_AndDeletingOneLeavesTheOther()
    {
        await using var db = Db();
        var store = new EfWorkspaceRecordStore(db);
        await store.SaveAsync(Bank, "brand", "current", "{\"who\":\"bank\"}");
        await store.SaveAsync(Insurer, "brand", "current", "{\"who\":\"insurer\"}");

        await store.DeleteAsync(Insurer, "brand", "current");

        Assert.Contains("bank", (await store.GetAsync(Bank, "brand", "current"))!.PayloadJson);
        Assert.Null(await store.GetAsync(Insurer, "brand", "current"));
    }

    [Fact]
    public async Task SavingAgainReplacesTheRecordInPlace()
    {
        await using var db = Db();
        var store = new EfWorkspaceRecordStore(db);
        await store.SaveAsync(Bank, "brand", "current", "{\"v\":1}");
        await store.SaveAsync(Bank, "brand", "current", "{\"v\":2}");

        var all = await store.ListAsync(Bank, "brand");
        Assert.Single(all);
        Assert.Contains("2", all[0].PayloadJson);
    }

    [Fact]
    public async Task AWorkspaceWithoutBrandSettingsUsesTheStartupDefault()
    {
        await using var db = Db();
        var service = new WorkspaceBrandService(new EfWorkspaceRecordStore(db));

        var brand = await service.ResolveAsync(Bank);

        Assert.Equal(KatoBrand.Current, brand);
        Assert.Null(await service.GetAsync(Bank));
    }

    [Fact]
    public async Task EachWorkspaceResolvesItsOwnBrand()
    {
        await using var db = Db();
        var service = new WorkspaceBrandService(new EfWorkspaceRecordStore(db));
        await service.SaveAsync(Bank, MavunoBank());

        var bank = await service.ResolveAsync(Bank);
        var other = await service.ResolveAsync(Insurer);

        Assert.Equal("Mavuno Bank", bank.Organisation);
        Assert.True(bank.IsBancassurance);
        Assert.NotEqual("Mavuno Bank", other.Organisation);
    }

    [Fact]
    public async Task TheBrandInUseFollowsTheWorkspaceEvenWhenTwoRunAtTheSameTime()
    {
        var bank = MavunoBank().ToBrand();
        var nile = new KatoBrand(MicroinsuranceCatalog.NileShield, "Nile", []);

        async Task<string> GreetAs(KatoBrand brand, int delayMs)
        {
            using var scope = KatoBrand.Use(brand);
            await Task.Delay(delayMs);
            return KatoReplies.Introduction("Eugene", voiceNotes: false);
        }

        var results = await Task.WhenAll(GreetAs(bank, 40), GreetAs(nile, 5));

        Assert.Contains("Mavuno's helper", results[0]);
        Assert.Contains("Nile's helper", results[1]);
        Assert.DoesNotContain("Mavuno", KatoReplies.Introduction("Eugene", voiceNotes: false));
    }

    [Fact]
    public void LeavingTheScopeRestoresThePreviousBrand()
    {
        var before = KatoBrand.Current;
        using (KatoBrand.Use(MavunoBank().ToBrand()))
        {
            Assert.Equal("Mavuno Bank", KatoBrand.Current.Organisation);
        }

        Assert.Equal(before, KatoBrand.Current);
    }

    [Fact]
    public void LifeAndGeneralWorkspacesSellDifferentCovers()
    {
        var life = MavunoBank() with { LineFocus = LineFocus.Life };
        var general = MavunoBank() with { LineFocus = LineFocus.General };
        var both = MavunoBank();

        var lifeProducts = life.ToBrand().Products;
        var generalProducts = general.ToBrand().Products;

        Assert.NotEmpty(lifeProducts);
        Assert.NotEmpty(generalProducts);
        Assert.All(lifeProducts, product => Assert.Equal(InsuranceClass.Life, product.InsuranceClass));
        Assert.All(generalProducts, product => Assert.Equal(InsuranceClass.General, product.InsuranceClass));
        Assert.Equal(lifeProducts.Count + generalProducts.Count, both.ToBrand().Products.Count);
    }

    [Fact]
    public void ABankSellsOnlyThePartnerInsurersCovers_AndNamesThem()
    {
        var products = MavunoBank().ToBrand().Products;

        Assert.All(products, product => Assert.Contains(product.Insurer, new[] { MicroinsuranceCatalog.Mpingo, MicroinsuranceCatalog.Kifaru }));
        Assert.DoesNotContain(products, product => product.Insurer == MicroinsuranceCatalog.NileShield);
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("   ", "x")]
    public void AnEmptyOrganisationNameIsRefused(string organisation, string shortName)
    {
        Assert.Throws<WorkspaceBrandException>(() =>
            WorkspaceBrandService.Validate(new WorkspaceBrandSettings(organisation, shortName, [], LineFocus.Both)));
    }

    [Fact]
    public void NamesAreTrimmed_ShortNameDefaultsToTheOrganisation_AndPartnersAreDeduplicated()
    {
        var clean = WorkspaceBrandService.Validate(
            new WorkspaceBrandSettings("  Mavuno Bank ", " ", [" Mpingo Life Assurance", "mpingo life assurance", "", "Kifaru General Insurance"], LineFocus.Both));

        Assert.Equal("Mavuno Bank", clean.Organisation);
        Assert.Equal("Mavuno Bank", clean.ShortName);
        Assert.Equal(2, clean.PartnerInsurers.Count);
    }

    [Fact]
    public void TooManyPartnersOrAnOverlongNameIsRefused()
    {
        var many = Enumerable.Range(0, WorkspaceBrandService.MaxPartners + 1).Select(i => $"Insurer {i}").ToArray();
        Assert.Throws<WorkspaceBrandException>(() => WorkspaceBrandService.Validate(new WorkspaceBrandSettings("Bank", "Bank", many, LineFocus.Both)));
        Assert.Throws<WorkspaceBrandException>(() =>
            WorkspaceBrandService.Validate(new WorkspaceBrandSettings(new string('a', WorkspaceBrandService.MaxNameLength + 1), "x", [], LineFocus.Both)));
    }
}
