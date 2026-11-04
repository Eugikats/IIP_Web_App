using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// With several workspaces on one deployment the assistant speaks for the workspace being served and sells that
/// workspace's covers (build-loop item 27): the organisation comes from the operating settings, the covers from the
/// published products that carry an offer. Nothing of another workspace, or of the built-in demonstration catalogue, leaks in
/// unless the runtime is a developer or demonstration one.
/// </summary>
public sealed partial class CustomerAssistantServiceTests
{
    private static readonly string[] OtherInsurersWords = ["Nile Shield", "Hospital Cash", "Boda Rider Accident", "Market Stall Stock"];

    private static Product OfferedCover(string tenantId, string name, string productClass, string insurer, string summary, ProductStatus status = ProductStatus.Published) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = TenantScope.Create(tenantId, "workspace-ca"),
            Version = 1,
            Name = name,
            ProductClass = productClass,
            Status = status,
            Stages = [],
            DocumentMatrix = [],
            UpdatedByUserId = "admin",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "test",
            Offer = new ProductOffer(
                insurer, summary, "Anyone in Uganda", "None, covered from day one", "The police report",
                [new ProductOfferLevel(2_000_000m, 5_000m)], "ZUR", "ZC", name),
        };

    private static IAssistantBrandProvider BrandProviderFor(
        string tenantId, string? organisation, string? shortName, bool builtInFallback = false, params Product[] products) =>
        new TenantAssistantBrandProvider(
            new SettingsFor(new Dictionary<string, TenantSettings>
            {
                [tenantId] = new() { Scope = TenantScope.Create(tenantId, "workspace-ca"), AssistantOrganisation = organisation, AssistantShortName = shortName },
            }),
            new ProductsFor(products),
            builtInFallback);

    [Fact]
    public async Task TheAssistantIntroducesItselfAsTheWorkspacesOrganisation_NotTheDeploymentDefault()
    {
        var fixture = Fixture.Create(
            tenantId: "tenant-mavuno",
            brandProvider: BrandProviderFor("tenant-mavuno", "Mavuno Bank", "Mavuno", false,
                OfferedCover("tenant-mavuno", "Mavuno Shop Cover", "Shop", "Zuri General Insurance", "Mavuno Shop Cover pays for your stock after a fire or a break-in.")));

        await fixture.HandleAsync("Hello");

        var reply = Assert.Single(fixture.Conversations.Saved).OutboundReplyText!;
        Assert.Contains("Mavuno's helper", reply);
        Assert.DoesNotContain("Nile Shield", reply);
    }

    [Fact]
    public async Task ACoverSuggestedToSomeoneWithoutCover_IsOneTheWorkspaceConfigured()
    {
        var fixture = Fixture.Create(
            tenantId: "tenant-mavuno",
            brandProvider: BrandProviderFor("tenant-mavuno", "Mavuno Bank", "Mavuno", false,
                OfferedCover("tenant-mavuno", "Mavuno Shop Cover", "Shop", "Zuri General Insurance", "Mavuno Shop Cover pays for your stock after a fire or a break-in.")));

        await fixture.HandleAsync("I sell tomatoes in Nakasero");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.LeadCreated, turn.Outcome);
        Assert.Contains("Mavuno Shop Cover pays for your stock", turn.OutboundReplyText);
        Assert.All(OtherInsurersWords, word => Assert.DoesNotContain(word, turn.OutboundReplyText));
        Assert.Contains("Mavuno Shop Cover", Assert.Single(fixture.SalesLeads.Created).ProductFit);
    }

    [Fact]
    public async Task AWorkspaceWithNothingToSell_SaysSo_AndAPersonFollowsUp_InsteadOfOfferingAnotherInsurersCovers()
    {
        var fixture = Fixture.Create(
            tenantId: "tenant-empty",
            brandProvider: BrandProviderFor("tenant-empty", "Empty Insurer", "Empty", false));

        await fixture.HandleAsync("I ride a boda boda in Kawempe");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.LeadCreated, turn.Outcome);
        Assert.Contains("do not have a cover to offer in this chat yet", turn.OutboundReplyText);
        Assert.Contains("Empty will get back to you", turn.OutboundReplyText);
        Assert.All(OtherInsurersWords, word => Assert.DoesNotContain(word, turn.OutboundReplyText));
    }

    [Fact]
    public async Task ADraftOrRetiredOfferIsNotSold()
    {
        var provider = BrandProviderFor("tenant-x", "X Insurer", "X", false,
            OfferedCover("tenant-x", "X Draft Cover", "Draft", "X", "A draft summary.", ProductStatus.Draft),
            OfferedCover("tenant-x", "X Retired Cover", "Retired", "X", "A retired summary.", ProductStatus.Retired),
            OfferedCover("tenant-x", "X Live Cover", "Live", "X", "A live summary."));

        var brand = await provider.ForAsync(TenantScope.Create("tenant-x", "workspace-ca"));

        Assert.Equal(["X Live Cover"], brand.Products.Select(product => product.Name));
    }

    [Fact]
    public async Task OneWorkspacesCoversAreNeverAnotherWorkspacesCovers()
    {
        var provider = new TenantAssistantBrandProvider(
            new SettingsFor(new Dictionary<string, TenantSettings>()),
            new ProductsFor(
                OfferedCover("tenant-one", "One Cover", "OneLine", "One Insurer", "Only for one."),
                OfferedCover("tenant-two", "Two Cover", "TwoLine", "Two Insurer", "Only for two.")),
            builtInCatalogueFallback: false);

        var one = await provider.ForAsync(TenantScope.Create("tenant-one", "workspace-ca"));
        var two = await provider.ForAsync(TenantScope.Create("tenant-two", "workspace-ca"));
        var three = await provider.ForAsync(TenantScope.Create("tenant-three", "workspace-ca"));

        Assert.Equal(["One Cover"], one.Products.Select(product => product.Name));
        Assert.Equal(["Two Cover"], two.Products.Select(product => product.Name));
        Assert.Empty(three.Products);
        Assert.Null(one.Find("Two Cover"));
        Assert.Null(one.Find("TwoLine"));
    }

    [Fact]
    public async Task OnlyADeveloperOrDemonstrationRuntimeFallsBackToTheBuiltInCatalogue()
    {
        var settings = new SettingsFor(new Dictionary<string, TenantSettings>());
        var products = new ProductsFor();
        var scope = TenantScope.Create("tenant-bare", "workspace-ca");

        var demonstration = await new TenantAssistantBrandProvider(settings, products, builtInCatalogueFallback: true).ForAsync(scope);
        var protectedRuntime = await new TenantAssistantBrandProvider(settings, products, builtInCatalogueFallback: false).ForAsync(scope);

        Assert.Contains(demonstration.Products, product => product.ProductLine == MicroinsuranceCatalog.HospitalCashLine);
        Assert.Empty(protectedRuntime.Products);
    }

    [Fact]
    public async Task TwoWorkspacesServedAtTheSameTimeEachHearTheirOwnOrganisation()
    {
        var mavuno = Fixture.Create(tenantId: "tenant-mavuno", brandProvider: BrandProviderFor("tenant-mavuno", "Mavuno Bank", "Mavuno"));
        var zuri = Fixture.Create(tenantId: "tenant-zuri", brandProvider: BrandProviderFor("tenant-zuri", "Zuri Assurance", "Zuri"));

        await Task.WhenAll(Enumerable.Range(0, 12).SelectMany(i => new[]
        {
            Task.Run(() => mavuno.HandleFromAsync("Hello", $"2567720000{i:00}")),
            Task.Run(() => zuri.HandleFromAsync("Hello", $"2567730000{i:00}")),
        }));

        Assert.All(mavuno.Conversations.Saved, turn =>
        {
            Assert.Contains("Mavuno's helper", turn.OutboundReplyText);
            Assert.DoesNotContain("Zuri", turn.OutboundReplyText);
        });
        Assert.All(zuri.Conversations.Saved, turn =>
        {
            Assert.Contains("Zuri's helper", turn.OutboundReplyText);
            Assert.DoesNotContain("Mavuno", turn.OutboundReplyText);
        });
        Assert.Equal(KatoBrand.Default, KatoBrand.Current); // nothing stays bound once the work is done
    }

    private sealed class SettingsFor(IReadOnlyDictionary<string, TenantSettings> byTenant) : ITenantSettingsRepository
    {
        public Task<TenantSettings> GetAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(byTenant.TryGetValue(scope.TenantId, out var settings) ? settings : new TenantSettings { Scope = scope });

        public Task SaveAsync(TenantSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ProductsFor(params Product[] products) : IProductRepository
    {
        public Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(products.FirstOrDefault(product => product.ProductId == productId && product.Scope.Matches(scope)));

        public Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);

        public Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>([]);

        public Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>(products.Where(product => product.Scope.Matches(scope)).ToArray());

        public Task SaveAsync(Product version, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
