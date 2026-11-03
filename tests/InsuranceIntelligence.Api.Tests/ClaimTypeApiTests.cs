using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 3 at HTTP level: configure claim types on a product, then file claims
/// against them from the staff form, the claimant portal and the suggestion endpoint.</summary>
public sealed class ClaimTypeApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly object[] MotorTypes =
    [
        new { name = "Own damage", description = "Damage to your own vehicle.", active = true, aliases = new[] { "accident", "collision" } },
        new { name = "Theft", description = "The vehicle was stolen.", active = true, aliases = new[] { "stolen" } },
        new { name = "Hail damage", description = "No longer offered.", active = false },
    ];

    [Fact]
    public async Task ProductClaimTypes_AreSavedWithDerivedKeysAndKeepTheirOrder()
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: MotorTypes);
        using var admin = factory.CreateScopedClient(tenant);

        var product = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));

        var types = product.GetProperty("claimTypes").EnumerateArray().ToArray();
        Assert.Equal(new[] { "own-damage", "theft", "hail-damage" }, types.Select(type => type.GetProperty("key").GetString()));
        Assert.Equal(new[] { 0, 1, 2 }, types.Select(type => type.GetProperty("sortOrder").GetInt32()));
        Assert.False(types[2].GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task ReorderingIsJustSendingTheListInANewOrder_AndEveryEditIsANewVersion()
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: MotorTypes);
        using var admin = factory.CreateScopedClient(tenant);
        var before = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));
        var versionBefore = before.GetProperty("version").GetInt32();

        var reordered = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new
        {
            changeReason = "reorder",
            claimTypes = new object[]
            {
                new { key = "theft", name = "Theft", active = true },
                new { key = "own-damage", name = "Own damage", active = true },
            },
        });

        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        var after = await MockedApiFactory.ReadJsonAsync(reordered);
        Assert.Equal(versionBefore + 1, after.GetProperty("version").GetInt32());
        Assert.Equal("Draft", after.GetProperty("status").GetString());
        Assert.Equal(new[] { "theft", "own-damage" }, after.GetProperty("claimTypes").EnumerateArray().Select(type => type.GetProperty("key").GetString()));
    }

    [Theory]
    [InlineData("duplicate-key")]
    [InlineData("duplicate-name")]
    [InlineData("bad-key")]
    [InlineData("blank-name")]
    public async Task InvalidClaimTypes_AreRefusedWithAPlainReason(string problem)
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant);
        using var admin = factory.CreateScopedClient(tenant);
        object[] types = problem switch
        {
            "duplicate-key" => [new { key = "a", name = "One", active = true }, new { key = "a", name = "Two", active = true }],
            "duplicate-name" => [new { name = "Theft", active = true }, new { name = "THEFT", active = true }],
            "bad-key" => [new { key = "Not A Key!", name = "One", active = true }],
            _ => [new { name = "  ", active = true }],
        };

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new { changeReason = "bad", claimTypes = types });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("claimTypes", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaffForm_AcceptsAConfiguredType_StoresItsKey_AndRefusesOthers()
    {
        var tenant = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenant, claimTypes: MotorTypes);
        using var admin = factory.CreateScopedClient(tenant);

        var accepted = await CreateClaimAsync(admin, "stolen");
        var refused = await CreateClaimAsync(admin, "Banana");
        var retired = await CreateClaimAsync(admin, "Hail damage");

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(accepted);
        Assert.Equal("Theft", body.GetProperty("claimType").GetString());
        Assert.Equal("theft", body.GetProperty("claimTypeKey").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Own damage", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
    }

    [Fact]
    public async Task ProductWithoutClaimTypes_StillAcceptsFreeText()
    {
        var tenant = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenant);
        using var admin = factory.CreateScopedClient(tenant);

        var response = await CreateClaimAsync(admin, "Anything at all");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("claimTypeKey").ValueKind);
    }

    [Fact]
    public async Task ClaimTypesList_ServesDropdowns_AndOnlyForTheCallersTenant()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenantA, claimTypes: MotorTypes);
        using var a = factory.CreateScopedClient(tenantA);
        using var b = factory.CreateScopedClient(tenantB);

        var listA = await MockedApiFactory.ReadJsonAsync(await a.GetAsync("/api/v1/claim-types?productLine=Motor"));
        var listB = await MockedApiFactory.ReadJsonAsync(await b.GetAsync("/api/v1/claim-types?productLine=Motor"));

        Assert.True(listA.GetProperty("configured").GetBoolean());
        Assert.Equal(new[] { "Own damage", "Theft" }, listA.GetProperty("claimTypes").EnumerateArray().Select(type => type.GetProperty("name").GetString()));
        Assert.False(listB.GetProperty("configured").GetBoolean());
        Assert.Empty(listB.GetProperty("claimTypes").EnumerateArray());
    }

    [Fact]
    public async Task Suggest_ReturnsABestGuess_NeverADecision()
    {
        var tenant = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenant, claimTypes: MotorTypes);
        using var admin = factory.CreateScopedClient(tenant);

        var hit = await admin.PostAsJsonAsync("/api/v1/claim-types/suggest", new { productLine = "Motor", text = "Thieves stole my car last night" });
        var miss = await admin.PostAsJsonAsync("/api/v1/claim-types/suggest", new { productLine = "Motor", text = "hello" });

        var hitBody = await MockedApiFactory.ReadJsonAsync(hit);
        Assert.True(hitBody.GetProperty("matched").GetBoolean());
        Assert.Equal("theft", hitBody.GetProperty("key").GetString());
        Assert.False((await MockedApiFactory.ReadJsonAsync(miss)).GetProperty("matched").GetBoolean());
    }

    [Fact]
    public async Task Claimant_SeesTheClaimTypesOfTheirOwnInsurerOnly_AndCanFileWithOne()
    {
        var tenant = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenant, claimTypes: MotorTypes);
        using var staff = factory.CreateScopedClient(tenant);
        var existing = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, existing);

        var list = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync("/api/v1/client-portal/claim-types?productLine=Motor"));

        Assert.Equal(new[] { "Own damage", "Theft" }, list.GetProperty("claimTypes").EnumerateArray().Select(type => type.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task ImportExisting_TurnsFreeTextTypesIntoConfiguredTypes_AndLinksOldClaims()
    {
        var tenant = MockedApiFactory.NewTenant();
        // A product with no claim types, and claims already filed with free text before this feature.
        var productId = await TestData.PublishProductAsync(factory, tenant);
        using var admin = factory.CreateScopedClient(tenant);
        var one = await TestData.CreateCaseAsync(admin, tenant, claimType: "Windscreen Damage");
        await TestData.CreateCaseAsync(admin, tenant, claimType: "windscreen damage");
        await TestData.CreateCaseAsync(admin, tenant, claimType: "ThirdPartyLiability");

        var imported = await admin.PostAsync($"/api/v1/products/{productId}/claim-types/import-existing", null);

        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var result = await MockedApiFactory.ReadJsonAsync(imported);
        Assert.Equal(2, result.GetProperty("addedClaimTypes").GetArrayLength());
        Assert.Equal(3, result.GetProperty("claimsUpdated").GetInt32());
        var claim = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{one.CaseId}"));
        Assert.Equal("windscreen-damage", claim.GetProperty("claimTypeKey").GetString());

        // Importing again changes nothing.
        var again = await MockedApiFactory.ReadJsonAsync(await admin.PostAsync($"/api/v1/products/{productId}/claim-types/import-existing", null));
        Assert.Equal(0, again.GetProperty("addedClaimTypes").GetArrayLength());
    }

    [Fact]
    public async Task ImportExisting_NeedsTheManageProductsPermission_AndTheOwnProduct()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenantA);
        using var officer = factory.CreateScopedClient(tenantA, role: "ClaimsOfficer");
        using var otherAdmin = factory.CreateScopedClient(tenantB);

        var forbidden = await officer.PostAsync($"/api/v1/products/{productId}/claim-types/import-existing", null);
        var foreign = await otherAdmin.PostAsync($"/api/v1/products/{productId}/claim-types/import-existing", null);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    private static Task<HttpResponseMessage> CreateClaimAsync(HttpClient client, string claimType) =>
        client.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Test Customer",
            customerEmail = "c@example.com",
            customerPhone = "+256700000009",
            policyNumber = "POL-" + Guid.NewGuid().ToString("N")[..6],
            productLine = "Motor",
            claimType,
            channel = "Phone",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"),
            incidentDescription = "Something happened.",
        });
}
