using System.Net;
using System.Net.Http.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 27 over HTTP: an administrator names the organisation Kato speaks for and describes the covers he may
/// explain, each workspace separately.</summary>
public sealed class AssistantBrandApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly object Offer = new
    {
        insurer = "Zuri General Insurance",
        summary = "Zuri Shop Cover pays for your stock after a fire or a break-in, up to UGX 2,000,000.",
        whoItIsFor = "Shop owners",
        waitingPeriod = "7 days after you join",
        papersForAClaim = "The police report",
        levels = new[] { new { sumInsured = 2_000_000m, monthlyPremium = 5_000m } },
        policyPrefix = "ZUR",
        code = "ZC",
        customerName = "Zuri Shop Cover",
    };

    [Fact]
    public async Task TheOrganisationKatoSpeaksForIsSavedPerWorkspaceAndAuditedByName()
    {
        var tenant = MockedApiFactory.NewTenant();
        var other = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        using var otherAdmin = factory.CreateScopedClient(other);

        var response = await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new
        {
            assistantOrganisation = "Mavuno Bank",
            assistantShortName = "Mavuno",
            assistantPartnerInsurers = "Zuri General Insurance",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Mavuno Bank", saved.GetProperty("assistantOrganisation").GetString());
        Assert.Equal("Mavuno", saved.GetProperty("assistantShortName").GetString());
        var elsewhere = await MockedApiFactory.ReadJsonAsync(await otherAdmin.GetAsync("/api/v1/admin/tenant-settings"));
        Assert.Equal(JsonValueKindNull, elsewhere.GetProperty("assistantOrganisation").ValueKind.ToString());
        var audit = await (await admin.GetAsync("/api/v1/audit/events")).Content.ReadAsStringAsync();
        Assert.Contains("tenant.settings.updated", audit);
    }

    [Fact]
    public async Task ABlankOrganisationGoesBackToTheDeploymentDefault_AndAHalfFilledBrandIsRefused()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { assistantOrganisation = "Mavuno Bank" });

        var cleared = await MockedApiFactory.ReadJsonAsync(await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { assistantOrganisation = " " }));
        var refused = await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { assistantPartnerInsurers = "Zuri General Insurance" });

        Assert.Equal(JsonValueKindNull, cleared.GetProperty("assistantOrganisation").ValueKind.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("organisation", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AProductCarriesTheOfferKatoExplains_AsPartOfItsVersion_OnlyInItsOwnWorkspace()
    {
        var tenant = MockedApiFactory.NewTenant();
        var other = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant);
        using var admin = factory.CreateScopedClient(tenant);

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new { changeReason = "Describe the cover", offer = Offer });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Draft", product.GetProperty("status").GetString()); // a changed published cover goes back through approval
        var offer = product.GetProperty("offer");
        Assert.Equal("Zuri General Insurance", offer.GetProperty("insurer").GetString());
        Assert.Equal("Zuri Shop Cover", offer.GetProperty("customerName").GetString());
        using var otherAdmin = factory.CreateScopedClient(other);
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.GetAsync($"/api/v1/products/{productId}")).StatusCode);
    }

    [Fact]
    public async Task AnIncompleteOfferIsRefusedWithAReason()
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant);
        using var admin = factory.CreateScopedClient(tenant);

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new
        {
            changeReason = "Describe the cover",
            offer = new { insurer = "Zuri", summary = "Pays.", whoItIsFor = "", waitingPeriod = "None", papersForAClaim = "A form", levels = new[] { new { sumInsured = 1m, monthlyPremium = 1m } } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("who the cover is for", await response.Content.ReadAsStringAsync());
    }

    private const string JsonValueKindNull = "Null";
}
