using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.DemoSeeding;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.LocalTestDoubles;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class MicroinsuranceDemoSeederTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-test", "workspace-test");

    private readonly DemoSeedInMemoryProductRepository products = new();
    private readonly InMemoryKnowledgeDocumentRepository knowledge = new();
    private readonly InMemoryKnowledgeIndexProvider index = new();
    private readonly InMemoryCustomerRepository customers = new();
    private readonly InMemoryPolicyRepository policies = new();
    private readonly InMemoryCaseRepository cases = new();

    private MicroinsuranceDemoSeeder Seeder(string name = "Demo Person") => new(
        products, knowledge, index, customers, policies, cases, new InMemoryAuditRepository(), new DemoPolicyholderSettings(name));

    [Fact]
    public async Task Catalogue_PublishesEveryLabelledMicroinsuranceProduct_WithEvidenceMatrices_AndIsRerunnable()
    {
        var first = await Seeder().SeedCatalogueAsync(Scope, CancellationToken.None);
        var second = await Seeder().SeedCatalogueAsync(Scope, CancellationToken.None);

        Assert.Equal(MicroinsuranceCatalog.All.Count, first.ProductsCreated);
        Assert.Equal(19, first.DocumentsIndexed);
        Assert.Equal(0, second.ProductsCreated);
        Assert.Equal(0, second.DocumentsIndexed);

        var published = await products.ListCurrentForAllProductsAsync(Scope, CancellationToken.None);
        Assert.Equal(
            MicroinsuranceCatalog.All.Select(p => p.ProductLine).Order().ToArray(),
            published.Select(p => p.ProductClass).Order().ToArray());
        Assert.All(published, product =>
        {
            Assert.Equal(ProductStatus.Published, product.Status);
            Assert.Contains("microinsurance", product.Name, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(MicroinsuranceDemoSeeder.DemoApprovalMarker, product.IraApprovalReference);
            Assert.Contains(product.DocumentMatrix, requirement => requirement.IsRequired);
        });
    }

    [Fact]
    public async Task Catalogue_KnowledgeIsFoundForItsOwnProductLine_AndStatesWaitingPeriodExclusionsAndGracePeriod()
    {
        await Seeder().SeedCatalogueAsync(Scope, CancellationToken.None);

        foreach (var line in MicroinsuranceCatalog.All.Select(p => p.ProductLine))
        {
            var documents = (await knowledge.ListAsync(Scope, CancellationToken.None)).Where(d => d.ProductLine == line).ToArray();
            Assert.True(documents.Length >= 3); // key information, wording, and how to join, pay and claim
            var text = string.Join(" ", documents.SelectMany(d => d.Chunks).Select(c => c.Content));
            Assert.Contains("microinsurance", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("waiting period", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("grace period", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not pay", text, StringComparison.OrdinalIgnoreCase);
            Assert.All(documents.SelectMany(d => d.Chunks), chunk => Assert.Equal(line, chunk.ProductLine));
        }

        var boda = await index.SearchAsync(Scope, "police report accident", MicroinsuranceCatalog.BodaAccidentLine, CancellationToken.None);
        Assert.NotEmpty(boda);
        Assert.All(boda, chunk => Assert.Equal(MicroinsuranceCatalog.BodaAccidentLine, chunk.ProductLine));
    }

    [Fact]
    public async Task Catalogue_ReplacesMadeUpIraReferences_WithANewVersionCarryingTheDemoMarker()
    {
        var legacy = new Product
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 1,
            Name = "Motor",
            ProductClass = "Motor",
            IraApprovalReference = "IRA/DEMO/MOTOR/2026",
            IraApprovalEffectiveDate = new DateOnly(2026, 1, 1),
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = [],
            UpdatedByUserId = "seed",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "seed",
        };
        await products.SaveAsync(legacy, CancellationToken.None);

        var result = await Seeder().SeedCatalogueAsync(Scope, CancellationToken.None);

        Assert.Equal(1, result.ProductsRelabelled);
        var current = await products.ListCurrentForAllProductsAsync(Scope, CancellationToken.None);
        Assert.DoesNotContain(current, p => p.IraApprovalReference?.StartsWith("IRA/", StringComparison.Ordinal) == true);
        Assert.Equal(2, current.Single(p => p.ProductId == legacy.ProductId).Version);
    }

    [Fact]
    public async Task DemoLoad_GivesThePhoneTwoMicroPolicies_AndAClaimWaitingForDocuments_OutsideTheDuplicateWindow()
    {
        var reply = await Seeder("Eugene Demo").LoadAsync(Scope, "256776678989", "corr", CancellationToken.None);
        await Seeder("Eugene Demo").LoadAsync(Scope, "256776678989", "corr", CancellationToken.None);

        Assert.Contains("#reset", reply, StringComparison.Ordinal);
        var customer = Assert.Single(await customers.ListAsync(Scope, CancellationToken.None));
        Assert.Equal("Eugene Demo", customer.FullName);
        Assert.True(PhoneNumbers.SameNumber("+256776678989", customer.Phone));

        var owned = await policies.ListByCustomerAsync(customer.CustomerId, Scope, CancellationToken.None);
        Assert.Equal(
            new[] { MicroinsuranceCatalog.BodaAccidentLine, MicroinsuranceCatalog.HospitalCashLine },
            owned.Select(p => p.ProductLine).Order().ToArray());
        Assert.All(owned, p => Assert.True(p.SumInsured < 10_000_000m));

        var claim = Assert.Single(await cases.ListAsync(Scope, CancellationToken.None));
        Assert.Equal(CaseStatus.MissingDocuments, claim.Status);
        Assert.True(claim.CreatedAt < DateTimeOffset.UtcNow.AddHours(-72));
        Assert.NotNull(claim.ProductId);
    }
}
