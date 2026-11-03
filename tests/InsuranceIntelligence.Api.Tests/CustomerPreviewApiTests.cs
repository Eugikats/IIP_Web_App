using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 8: "Preview as customer" shows what a customer would be asked for a
/// chosen claim type, from a draft or published product, and stores or sends nothing.</summary>
public sealed class CustomerPreviewApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly object[] ClaimTypes =
    [
        new { name = "Accident", description = "Collision or impact", active = true, sortOrder = 1 },
        new { name = "Theft", description = "Stolen vehicle", active = true, sortOrder = 2 },
        new { name = "Retired type", description = "No longer offered", active = false, sortOrder = 3 },
    ];

    private static object Matrix(string key, string label, string[]? claimTypeKeys = null, string kind = "Document") => new
    {
        documentKey = key,
        customerLabel = label,
        reason = "Needed to assess the claim",
        isRequired = true,
        requiredAtStageKey = "acknowledgement_and_document_request",
        isRegulatoryOrWordingMandated = false,
        claimTypeKeys,
        kind,
    };

    private async Task<(HttpClient Admin, Guid ProductId)> ArrangeAsync(bool configure = true)
    {
        var tenant = MockedApiFactory.NewTenant();
        var admin = factory.CreateScopedClient(tenant);
        var created = await admin.PostAsJsonAsync("/api/v1/products", new { name = "Motor Cover", productClass = "Motor", changeReason = "new" });
        var productId = (await MockedApiFactory.ReadJsonAsync(created)).GetProperty("productId").GetGuid();
        if (configure)
        {
            var updated = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new
            {
                claimTypes = ClaimTypes,
                documentMatrix = new[]
                {
                    Matrix("ClaimForm", "Claim form"),
                    Matrix("PoliceAbstract", "Police report", ["theft"]),
                    Matrix("PhotoEvidence", "Photos of the damage", ["accident"], "Evidence"),
                },
                claimFields = new object[]
                {
                    new { label = "Where did it happen", type = "Text", required = true, sortOrder = 1, customerVisible = true, claimTypeKeys = Array.Empty<string>() },
                    new { label = "Internal triage note", type = "Text", required = false, sortOrder = 2, customerVisible = false, claimTypeKeys = Array.Empty<string>() },
                    new { label = "Was the car driveable", type = "YesNo", required = false, sortOrder = 3, customerVisible = true, claimTypeKeys = new[] { "accident" } },
                },
                changeReason = "configure",
            });
            Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());
        }

        return (admin, productId);
    }

    private static async Task<JsonElement> PreviewAsync(HttpClient admin, Guid productId, string? claimType = null)
    {
        var url = $"/api/v1/products/{productId}/customer-preview" + (claimType is null ? string.Empty : $"?claimType={Uri.EscapeDataString(claimType)}");
        var response = await admin.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await MockedApiFactory.ReadJsonAsync(response);
    }

    private static string[] Labels(JsonElement preview) =>
        preview.GetProperty("documents").EnumerateArray().Select(document => document.GetProperty("label").GetString()!).ToArray();

    [Fact]
    public async Task ADraftProductCanBePreviewedForEachClaimType()
    {
        var (admin, productId) = await ArrangeAsync();

        var theft = await PreviewAsync(admin, productId, "Theft");
        var accident = await PreviewAsync(admin, productId, "accident");

        Assert.False(theft.GetProperty("isPublished").GetBoolean());
        Assert.Equal("Draft", theft.GetProperty("status").GetString());
        Assert.Equal("theft", theft.GetProperty("claimTypeKey").GetString());
        Assert.Equal(["Claim form", "Police report"], Labels(theft));
        Assert.Equal(["Claim form", "Photos of the damage"], Labels(accident));
        Assert.Equal("Evidence", accident.GetProperty("documents").EnumerateArray().Last().GetProperty("kind").GetString());
        Assert.All(accident.GetProperty("documents").EnumerateArray(), document =>
        {
            Assert.False(document.GetProperty("uploaded").GetBoolean());
            Assert.Equal("Not uploaded", document.GetProperty("status").GetString());
        });
    }

    [Fact]
    public async Task OnlyQuestionsTheCustomerWouldSeeAreShownForTheClaimType()
    {
        var (admin, productId) = await ArrangeAsync();

        var theft = await PreviewAsync(admin, productId, "Theft");
        var accident = await PreviewAsync(admin, productId, "Accident");

        string[] Questions(JsonElement preview) => preview.GetProperty("form").GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("label").GetString()!).ToArray();
        Assert.Equal(["Where did it happen"], Questions(theft));
        Assert.Equal(["Where did it happen", "Was the car driveable"], Questions(accident));
        Assert.True(accident.GetProperty("form").GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task WithNoClaimTypeChosenTheFirstActiveOneIsUsedAndRetiredOnesAreNotOffered()
    {
        var (admin, productId) = await ArrangeAsync();

        var preview = await PreviewAsync(admin, productId);

        Assert.Equal("Accident", preview.GetProperty("claimTypeName").GetString());
        Assert.Equal(["Accident", "Theft"], preview.GetProperty("claimTypes").EnumerateArray().Select(type => type.GetProperty("name").GetString()!).ToArray());
        Assert.Equal("POL-SAMPLE-0001", preview.GetProperty("samplePolicyNumber").GetString());
    }

    [Fact]
    public async Task AProductWithNothingConfiguredPreviewsTheBuiltInRules()
    {
        var (admin, productId) = await ArrangeAsync(configure: false);

        var preview = await PreviewAsync(admin, productId);

        Assert.Equal(0, preview.GetProperty("claimTypes").GetArrayLength());
        Assert.NotEmpty(Labels(preview));
        Assert.True(preview.GetProperty("form").GetProperty("fields").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ANameThatIsNotAClaimTypeOfTheProductIsRefused()
    {
        var (admin, productId) = await ArrangeAsync();

        var response = await admin.GetAsync($"/api/v1/products/{productId}/customer-preview?claimType=Flood");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no claim type called 'Flood'", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PreviewingStoresNothingAndSendsNothing()
    {
        var (admin, productId) = await ArrangeAsync();
        var casesBefore = (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/cases"))).ToString();
        var outboxBefore = await admin.GetStringAsync("/api/v1/dev/mock-outbox");
        var auditBefore = await admin.GetStringAsync("/api/v1/audit/events");
        var versionBefore = (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"))).GetProperty("version").GetInt32();

        await PreviewAsync(admin, productId, "Theft");
        await PreviewAsync(admin, productId, "Accident");

        Assert.Equal(casesBefore, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/cases"))).ToString());
        Assert.Equal(outboxBefore, await admin.GetStringAsync("/api/v1/dev/mock-outbox"));
        Assert.Equal(auditBefore, await admin.GetStringAsync("/api/v1/audit/events"));
        Assert.Equal(versionBefore, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"))).GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("CustomerService")]
    [InlineData("Broker")]
    public async Task OnlyProductManagersMayPreview(string role)
    {
        var (admin, productId) = await ArrangeAsync();
        using var other = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/products/{productId}/customer-preview")).StatusCode);
        admin.Dispose();
    }

    [Fact]
    public async Task AnotherWorkspaceCannotPreviewAProduct()
    {
        var (admin, productId) = await ArrangeAsync();
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/products/{productId}/customer-preview")).StatusCode);
        admin.Dispose();
    }
}
