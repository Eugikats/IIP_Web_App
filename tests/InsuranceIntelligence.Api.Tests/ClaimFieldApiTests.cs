using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 5 at HTTP level: configure claim fields on a product, file a claim
/// with answers, read and change them as staff and as the claimant.</summary>
public sealed class ClaimFieldApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly object[] ClaimTypes =
    [
        new { name = "Own damage", active = true },
        new { name = "Theft", active = true },
    ];

    private static object Field(string label, string type, bool required = false, bool customer = true, string[]? options = null, string[]? claimTypes = null, string? key = null) =>
        new { key, label, type, required, options = options ?? [], helpText = (string?)null, sortOrder = 0, customerVisible = customer, claimTypeKeys = claimTypes ?? [] };

    private async Task<string> ConfigureAsync(params object[] fields)
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes);
        using var admin = factory.CreateScopedClient(tenant);
        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new { changeReason = "fields", claimFields = fields });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        // Editing returns the product to draft; publish again with a second person.
        using var maker = factory.CreateScopedClient(tenant, userId: "maker-1");
        using var checker = factory.CreateScopedClient(tenant, userId: "checker-1");
        Assert.True((await maker.PostAsJsonAsync($"/api/v1/products/{productId}/submit", new { changeReason = "go" })).IsSuccessStatusCode);
        Assert.True((await checker.PostAsJsonAsync($"/api/v1/products/{productId}/approve", new { changeReason = "ok" })).IsSuccessStatusCode);
        return tenant;
    }

    private static Task<HttpResponseMessage> CreateClaimAsync(HttpClient client, object? fieldValues, string claimType = "Own damage") =>
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
            fieldValues,
        });

    [Fact]
    public async Task ANewMotorProduct_StartsWithTheBuiltInQuestions_ThatMirrorTheOriginalForm()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);

        var created = await MockedApiFactory.ReadJsonAsync(await admin.PostAsJsonAsync("/api/v1/products", new { name = "Motor Cover", productClass = "Motor", changeReason = "new" }));

        var keys = created.GetProperty("claimFields").EnumerateArray().Select(field => field.GetProperty("key").GetString()).ToArray();
        Assert.Contains("vehicleRegistration", keys);
        Assert.Contains("injuryIndicator", keys);
        var other = await MockedApiFactory.ReadJsonAsync(await admin.PostAsJsonAsync("/api/v1/products", new { name = "Marine", productClass = "Marine", changeReason = "new" }));
        Assert.Empty(other.GetProperty("claimFields").EnumerateArray());
    }

    [Fact]
    public async Task TheBuilderSavesFieldsWithDerivedKeys_InTheOrderGiven()
    {
        var tenant = await ConfigureAsync(
            Field("Police report number", "Text", required: true),
            Field("Estimated repair cost", "Currency"),
            Field("Was the car driveable?", "YesNo"));
        using var admin = factory.CreateScopedClient(tenant);
        var list = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/products"));
        var productId = list.EnumerateArray().First().GetProperty("productId").GetGuid();

        var product = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));

        var fields = product.GetProperty("claimFields").EnumerateArray().ToArray();
        Assert.Equal(new[] { "policeReportNumber", "estimatedRepairCost", "wasTheCarDriveable" }, fields.Select(field => field.GetProperty("key").GetString()));
        Assert.Equal(new[] { 0, 1, 2 }, fields.Select(field => field.GetProperty("sortOrder").GetInt32()));
    }

    [Theory]
    [InlineData("duplicate-key")]
    [InlineData("bad-type")]
    [InlineData("select-without-options")]
    [InlineData("bad-key")]
    [InlineData("unknown-claim-type")]
    [InlineData("blank-label")]
    public async Task InvalidFieldDefinitions_AreRefused(string problem)
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes);
        using var admin = factory.CreateScopedClient(tenant);
        object[] fields = problem switch
        {
            "duplicate-key" => [Field("A", "Text", key: "same"), Field("B", "Text", key: "same")],
            "bad-type" => [Field("A", "Telepathy")],
            "select-without-options" => [Field("A", "Select", options: ["only-one"])],
            "bad-key" => [Field("A", "Text", key: "1 bad key")],
            "unknown-claim-type" => [Field("A", "Text", claimTypes: ["marine"])],
            _ => [Field(" ", "Text")],
        };

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new { changeReason = "bad", claimFields = fields });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("claimFields", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AClaimTypeStillUsedByAField_CannotBeRemoved()
    {
        var tenant = await ConfigureAsync(Field("Stolen item", "Text", claimTypes: ["theft"]));
        using var admin = factory.CreateScopedClient(tenant);
        var productId = (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/products"))).EnumerateArray().First().GetProperty("productId").GetGuid();

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new { changeReason = "drop", claimTypes = new object[] { new { name = "Own damage", active = true } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Stolen item", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheFormForAClaimType_ListsOnlyItsFields_AndAClaimantFormOnlyTheCustomerVisibleOnes()
    {
        var tenant = await ConfigureAsync(
            Field("Contact name", "Text"),
            Field("Stolen item", "Text", claimTypes: ["theft"]),
            Field("Assessor notes", "Text", customer: false));
        using var staff = factory.CreateScopedClient(tenant);

        var theft = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/claim-fields?productLine=Motor&claimType=Theft"));
        var damage = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/claim-fields?productLine=Motor&claimType=Own%20damage"));

        Assert.True(theft.GetProperty("configured").GetBoolean());
        Assert.Equal(new[] { "contactName", "stolenItem", "assessorNotes" }, theft.GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("key").GetString()));
        Assert.Equal(new[] { "contactName", "assessorNotes" }, damage.GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("key").GetString()));

        var claim = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);
        var customerForm = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}/fields"));
        Assert.Equal(new[] { "contactName", "stolenItem" }, customerForm.GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("key").GetString()));
    }

    [Fact]
    public async Task FilingAClaim_RequiresRequiredFields_RefusesBadAnswers_AndStoresGoodOnes()
    {
        var tenant = await ConfigureAsync(
            Field("Police report number", "Text", required: true),
            Field("Repair estimate", "Currency"));
        using var staff = factory.CreateScopedClient(tenant);

        var missing = await CreateClaimAsync(staff, new Dictionary<string, string>());
        var bad = await CreateClaimAsync(staff, new Dictionary<string, string> { ["policeReportNumber"] = "P1", ["repairEstimate"] = "lots" });
        var good = await CreateClaimAsync(staff, new Dictionary<string, string> { ["policeReportNumber"] = "P1", ["repairEstimate"] = "1,500,000" });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("fieldValues.policeReportNumber", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("fieldValues.repairEstimate", await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(good);
        Assert.Equal("P1", body.GetProperty("fieldValues").GetProperty("policeReportNumber").GetString());
        Assert.Equal("1500000", body.GetProperty("fieldValues").GetProperty("repairEstimate").GetString());
    }

    [Fact]
    public async Task AnswersToTheBuiltInInjuryQuestion_StillDriveRouting()
    {
        var tenant = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes);
        using var staff = factory.CreateScopedClient(tenant);

        var response = await CreateClaimAsync(staff, new Dictionary<string, string> { ["injuryIndicator"] = "Injury reported" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Injury reported", body.GetProperty("motorFnolDetails").GetProperty("injuryIndicator").GetString());
    }

    [Fact]
    public async Task Staff_CanSaveSomeAnswersLater_AndOnlyWhichFieldsChangedIsAudited()
    {
        var tenant = await ConfigureAsync(Field("Police report number", "Text", required: true), Field("Estimated cost", "Number"));
        using var staff = factory.CreateScopedClient(tenant);
        var created = await CreateClaimAsync(staff, new Dictionary<string, string> { ["policeReportNumber"] = "P1" });
        var caseId = (await MockedApiFactory.ReadJsonAsync(created)).GetProperty("caseId").GetGuid();

        var saved = await staff.PutAsJsonAsync($"/api/v1/cases/{caseId}/field-values", new { values = new Dictionary<string, string> { ["estimatedCost"] = "2500000" } });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var form = await MockedApiFactory.ReadJsonAsync(saved);
        var answers = form.GetProperty("fields").EnumerateArray().ToDictionary(field => field.GetProperty("key").GetString()!, field => field.GetProperty("value").GetString());
        Assert.Equal("P1", answers["policeReportNumber"]);
        Assert.Equal("2500000", answers["estimatedCost"]);
        var claim = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}"));
        Assert.Equal("case.fields.updated", claim.GetProperty("audit").GetProperty("lastActionName").GetString());
        Assert.DoesNotContain("2500000", claim.GetProperty("audit").GetRawText());
    }

    [Fact]
    public async Task TheClaimant_CanAnswerCustomerVisibleFields_ButNotStaffOnesOrOthers()
    {
        var tenant = await ConfigureAsync(Field("Contact name", "Text"), Field("Assessor notes", "Text", customer: false));
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var allowed = await claimant.PutAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/field-values", new { values = new Dictionary<string, string> { ["contactName"] = "Amina" } });
        var staffOnly = await claimant.PutAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/field-values", new { values = new Dictionary<string, string> { ["assessorNotes"] = "sneaky" } });

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, staffOnly.StatusCode);
        var stored = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}"));
        Assert.Equal("Amina", stored.GetProperty("fieldValues").GetProperty("contactName").GetString());
        Assert.False(stored.GetProperty("fieldValues").TryGetProperty("assessorNotes", out _));
    }

    [Fact]
    public async Task AnotherTenant_CannotReadOrChangeTheAnswers()
    {
        var tenantA = await ConfigureAsync(Field("Police report number", "Text"));
        var tenantB = MockedApiFactory.NewTenant();
        using var staffA = factory.CreateScopedClient(tenantA);
        using var staffB = factory.CreateScopedClient(tenantB);
        var created = await CreateClaimAsync(staffA, new Dictionary<string, string> { ["policeReportNumber"] = "P1" });
        var caseId = (await MockedApiFactory.ReadJsonAsync(created)).GetProperty("caseId").GetGuid();

        var read = await staffB.GetAsync($"/api/v1/cases/{caseId}/fields");
        var write = await staffB.PutAsJsonAsync($"/api/v1/cases/{caseId}/field-values", new { values = new Dictionary<string, string> { ["policeReportNumber"] = "HACKED" } });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task AClaimOnAProductWithNoConfiguredFields_ReadsItsOriginalFnolAnswersThroughTheBuiltInQuestions()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var created = await staff.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Old Style", customerEmail = "o@example.com", customerPhone = "+256700000010", policyNumber = "POL-OLD1",
            productLine = "Motor", claimType = "Own damage", channel = "Phone",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"), incidentDescription = "Old.",
            motorFnolDetails = new { vehicleRegistration = "UAX 123A", accidentLocation = "Jinja Road" },
        });
        var caseId = (await MockedApiFactory.ReadJsonAsync(created)).GetProperty("caseId").GetGuid();

        var form = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/fields"));

        Assert.False(form.GetProperty("configured").GetBoolean());
        var answers = form.GetProperty("fields").EnumerateArray().ToDictionary(field => field.GetProperty("key").GetString()!, field => field.GetProperty("value").GetString());
        Assert.Equal("UAX 123A", answers["vehicleRegistration"]);
        Assert.Equal("Jinja Road", answers["accidentLocation"]);
    }
}
