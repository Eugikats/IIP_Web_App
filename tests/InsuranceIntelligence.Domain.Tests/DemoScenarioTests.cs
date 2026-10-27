using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.DemoScenarios;
using InsuranceIntelligence.Application.DemoSeeding;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.LocalTestDoubles;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class DemoScenarioTests
{
    private static readonly TenantScope Scope = TenantScope.Create("org-demo", "main");

    private readonly DemoSeedInMemoryProductRepository products = new();
    private readonly InMemoryKnowledgeDocumentRepository knowledge = new();
    private readonly InMemoryKnowledgeIndexProvider index = new();
    private readonly InMemoryCustomerRepository customers = new();
    private readonly InMemoryPolicyRepository policies = new();

    private MicroinsuranceDemoSeeder Catalogue() => new(
        products, knowledge, index, customers, policies, new InMemoryCaseRepository(), new InMemoryAuditRepository(), new DemoPolicyholderSettings("Demo Person"));

    private static KatoBrand BrandOf(string id)
    {
        var scenario = DemoScenarioCatalog.Find(id)!;
        return new KatoBrand(scenario.BrandName, scenario.ShortName, scenario.PartnerInsurers, scenario.LineFocus);
    }

    [Fact]
    public void ThereAreThreeScenarios_WithTheAgreedNames()
    {
        Assert.Equal(["Mavuno Bank", "Nile Life", "Nile General"], DemoScenarioCatalog.All.Select(item => item.Title).ToArray());
        Assert.True(DemoScenarioCatalog.Find("mavuno-bank")!.IsBank);
        Assert.False(DemoScenarioCatalog.Find("nile-life")!.IsBank);
        Assert.Null(DemoScenarioCatalog.Find("nobody"));
    }

    [Fact]
    public void EachScenarioSellsOnlyItsOwnCovers()
    {
        var bank = BrandOf(DemoScenarioCatalog.MavunoBank).Products;
        var life = BrandOf(DemoScenarioCatalog.NileLife).Products;
        var general = BrandOf(DemoScenarioCatalog.NileGeneral).Products;

        Assert.All(bank, product => Assert.Contains(product.Insurer, new[] { MicroinsuranceCatalog.Mpingo, MicroinsuranceCatalog.Kifaru }));
        Assert.Contains(bank, product => product.InsuranceClass == InsuranceClass.Life);
        Assert.Contains(bank, product => product.InsuranceClass == InsuranceClass.General);

        Assert.Equal(3, life.Count);
        Assert.All(life, product =>
        {
            Assert.Equal(MicroinsuranceCatalog.NileLife, product.Insurer);
            Assert.Equal(InsuranceClass.Life, product.InsuranceClass);
            Assert.StartsWith("Nile Shield Life", product.Name);
        });

        Assert.NotEmpty(general);
        Assert.All(general, product =>
        {
            Assert.Equal(MicroinsuranceCatalog.NileShield, product.Insurer);
            Assert.Equal(InsuranceClass.General, product.InsuranceClass);
        });
    }

    [Fact]
    public void ALookupByProductLineFindsTheCoverOfTheOrganisationAsking()
    {
        var nileLife = BrandOf(DemoScenarioCatalog.NileLife);
        var bank = BrandOf(DemoScenarioCatalog.MavunoBank);

        Assert.Equal(MicroinsuranceCatalog.NileLife, nileLife.Find(MicroinsuranceCatalog.LoanProtectorLine)!.Insurer);
        Assert.Equal(MicroinsuranceCatalog.Mpingo, bank.Find(MicroinsuranceCatalog.LoanProtectorLine)!.Insurer);
        Assert.Null(nileLife.Find(MicroinsuranceCatalog.BodaAccidentLine));
    }

    [Fact]
    public async Task ANileLifeWorkspaceHoldsItsLifeCoversAndWordingsAndNoOtherInsurers()
    {
        var result = await Catalogue().SeedCatalogueForAsync(Scope, BrandOf(DemoScenarioCatalog.NileLife).Products, CancellationToken.None);

        Assert.Equal(3, result.ProductsCreated);
        Assert.Equal(9, result.DocumentsIndexed);
        var published = await products.ListCurrentForAllProductsAsync(Scope, CancellationToken.None);
        Assert.All(published, product => Assert.Contains("Nile Shield Life", product.Name));

        var text = string.Join(" ", (await knowledge.ListAsync(Scope, CancellationToken.None)).SelectMany(document => document.Chunks).Select(chunk => chunk.Content));
        Assert.Contains("Nile Shield Life Assurance", text);
        Assert.DoesNotContain("Mpingo", text);
        Assert.DoesNotContain("Kifaru", text);
    }

    [Fact]
    public async Task ABankWorkspaceHoldsItsPartnersCoversOnly_AndSeedingAgainAddsNothing()
    {
        var bank = BrandOf(DemoScenarioCatalog.MavunoBank).Products;
        var first = await Catalogue().SeedCatalogueForAsync(Scope, bank, CancellationToken.None);
        var second = await Catalogue().SeedCatalogueForAsync(Scope, bank, CancellationToken.None);

        Assert.Equal(bank.Count, first.ProductsCreated);
        Assert.Equal(0, second.ProductsCreated);
        Assert.Equal(0, second.DocumentsIndexed);
        var text = string.Join(" ", (await knowledge.ListAsync(Scope, CancellationToken.None)).SelectMany(document => document.Chunks).Select(chunk => chunk.Content));
        Assert.DoesNotContain("Nile Shield", text);
        Assert.Contains("Mpingo Life Assurance", text);
    }

    [Theory]
    [InlineData(DemoScenarioCatalog.NileLife)]
    [InlineData(DemoScenarioCatalog.NileGeneral)]
    public async Task PolicyDatesAreWorkedOutFromToday_AndRunningAgainAddsNoDuplicates(string id)
    {
        var seeder = new DemoScenarioPolicyholderSeeder(customers, policies);
        var scenario = DemoScenarioCatalog.Find(id)!;

        var first = await seeder.SeedAsync(Scope, scenario);
        var second = await seeder.SeedAsync(Scope, scenario);

        Assert.Equal(12, first.Policies);
        Assert.Equal(first, second);
        var all = await policies.ListAsync(Scope, CancellationToken.None);
        Assert.Equal(12, all.Count);
        Assert.Equal(12, (await customers.ListAsync(Scope, CancellationToken.None)).Count);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysToEnd = all.Select(policy => policy.EffectiveTo!.Value.DayNumber - today.DayNumber).Order().ToArray();
        Assert.Contains(3, daysToEnd);
        Assert.Contains(30, daysToEnd);
        Assert.Contains(-6, daysToEnd);
        Assert.All(all, policy => Assert.True(policy.EffectiveTo > policy.EffectiveFrom));
    }

    [Fact]
    public async Task ABankScenarioLoadsNoPolicyholders_BecauseItsPeopleHaveNoCoverYet()
    {
        var seeder = new DemoScenarioPolicyholderSeeder(customers, policies);

        var result = await seeder.SeedAsync(Scope, DemoScenarioCatalog.Find(DemoScenarioCatalog.MavunoBank)!);

        Assert.Equal(0, result.Policies);
        Assert.Empty(await policies.ListAsync(Scope, CancellationToken.None));
    }

    [Fact]
    public async Task OneScenarioNeverWritesIntoAnotherOrganisation()
    {
        var seeder = new DemoScenarioPolicyholderSeeder(customers, policies);
        var other = TenantScope.Create("org-other", "main");

        await seeder.SeedAsync(Scope, DemoScenarioCatalog.Find(DemoScenarioCatalog.NileGeneral)!);

        Assert.Empty(await policies.ListAsync(other, CancellationToken.None));
        Assert.Empty(await customers.ListAsync(other, CancellationToken.None));
    }
}
