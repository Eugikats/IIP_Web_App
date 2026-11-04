using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 26: a returning customer is offered what they already gave, only their own, only in their own
/// workspace, and only to accept.</summary>
public sealed class CustomerMemoryApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static async Task<CaseInfo> CreateClaimAsync(HttpClient staff, string tenant, string phone, string? email = null, string productLine = "Motor", string claimType = "Own damage")
    {
        var contactEmail = email ?? $"memory-{Guid.NewGuid():N}"[..18] + "@example.com";
        var policy = "POL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var response = await staff.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Eugene Katusiime",
            customerEmail = contactEmail,
            customerPhone = phone,
            policyNumber = policy,
            productLine,
            claimType,
            channel = "Phone",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            incidentDescription = "Collision on the highway.",
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var caseId = (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("caseId").GetGuid();
        return new CaseInfo(caseId, policy, contactEmail, phone, tenant, "workspace-main");
    }

    private static string NewPhone() => "+2567" + Random.Shared.Next(10_000_000, 99_999_999);

    private static async Task SaveDetailsAsync(HttpClient claimant, Guid caseId, Dictionary<string, string> values)
    {
        var response = await claimant.PutAsJsonAsync($"/api/v1/client-portal/claims/{caseId}/field-values", new { values });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task UploadAsync(HttpClient claimant, Guid caseId, string slotKey, string fileName, byte[] bytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent(slotKey), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> OnFileAsync(HttpClient claimant, Guid caseId) =>
        await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{caseId}/on-file"));

    private static string Ref(Guid caseId) => $"CLM-{caseId.ToString("N")[..8].ToUpperInvariant()}";

    [Fact]
    public async Task AFirstClaimHasNothingOnFile()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await CreateClaimAsync(staff, tenant, NewPhone());
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var offered = await OnFileAsync(claimant, claim.CaseId);

        Assert.Empty(offered.GetProperty("values").EnumerateArray());
        Assert.Empty(offered.GetProperty("documents").EnumerateArray());
    }

    [Fact]
    public async Task DetailsFromAnEarlierClaimAreOfferedAndUsedOnlyWhenTheCustomerAccepts()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var phone = NewPhone();
        var first = await CreateClaimAsync(staff, tenant, phone);
        using (var claimantA = await TestData.ClaimantClientAsync(factory, staff, first))
        {
            await SaveDetailsAsync(claimantA, first.CaseId, new() { ["drivingPermitNumber"] = "DP-778899", ["accidentLocation"] = "Jinja Road" });
        }

        var second = await CreateClaimAsync(staff, tenant, phone);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, second);
        var offered = await OnFileAsync(claimant, second.CaseId);

        var value = Assert.Single(offered.GetProperty("values").EnumerateArray());
        Assert.Equal("drivingPermitNumber", value.GetProperty("key").GetString());
        Assert.Equal("Driving permit number", value.GetProperty("label").GetString());
        Assert.Equal("DP-778899", value.GetProperty("value").GetString());
        Assert.Equal(Ref(first.CaseId), value.GetProperty("fromClaim").GetString());

        // Offered, not filled in: the new claim still has no answer until the customer says so.
        var before = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{second.CaseId}/fields"));
        Assert.Equal(JsonValueKind.Null, before.GetProperty("fields").EnumerateArray().Single(field => field.GetProperty("key").GetString() == "drivingPermitNumber").GetProperty("value").ValueKind);

        var use = await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{second.CaseId}/on-file/use-details", new { keys = new[] { "drivingPermitNumber" } });
        Assert.Equal(HttpStatusCode.OK, use.StatusCode);
        var after = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{second.CaseId}/fields"));
        Assert.Equal("DP-778899", after.GetProperty("fields").EnumerateArray().Single(field => field.GetProperty("key").GetString() == "drivingPermitNumber").GetProperty("value").GetString());
        Assert.Empty((await OnFileAsync(claimant, second.CaseId)).GetProperty("values").EnumerateArray());
        Assert.Contains("customer_memory.used", await staff.GetStringAsync("/api/v1/audit/events?pageSize=200"));
    }

    [Fact]
    public async Task AskingForADetailThatIsNotOnFileIsRefusedAndNothingIsChanged()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await CreateClaimAsync(staff, tenant, NewPhone());
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        Assert.Equal(HttpStatusCode.Conflict, (await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/on-file/use-details", new { keys = new[] { "drivingPermitNumber" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/on-file/use-details", new { keys = Array.Empty<string>() })).StatusCode);
    }

    [Fact]
    public async Task ADocumentFromAnEarlierClaimIsOfferedAndCopiedIntoTheNewClaimOnlyWhenChosen()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var phone = NewPhone();
        var first = await CreateClaimAsync(staff, tenant, phone);
        var idBytes = new byte[2500];
        new Random(21).NextBytes(idBytes);
        using (var claimantA = await TestData.ClaimantClientAsync(factory, staff, first))
        {
            await UploadAsync(claimantA, first.CaseId, "CustomerId", "id_card.jpg", idBytes, "image/jpeg");
        }

        var second = await CreateClaimAsync(staff, tenant, phone);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, second);
        var offered = Assert.Single((await OnFileAsync(claimant, second.CaseId)).GetProperty("documents").EnumerateArray());
        Assert.Equal("CustomerId", offered.GetProperty("documentTypeKey").GetString());
        Assert.Equal(Ref(first.CaseId), offered.GetProperty("fromClaim").GetString());
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{second.CaseId}/documents"))).EnumerateArray());

        var use = await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{second.CaseId}/on-file/use-document", new { sourceDocumentId = offered.GetProperty("documentId").GetGuid(), documentTypeKey = "CustomerId" });

        Assert.Equal(HttpStatusCode.OK, use.StatusCode);
        var copy = Assert.Single((await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{second.CaseId}/documents"))).EnumerateArray());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(idBytes)).ToLowerInvariant(), copy.GetProperty("sha256").GetString());
        Assert.Equal("id_card.jpg", copy.GetProperty("originalFileName").GetString());
        Assert.Equal(Ref(first.CaseId), copy.GetProperty("reusedFromClaim").GetString());
        // The earlier claim keeps its own copy, and the offer is gone now that the slot is filled.
        Assert.Single((await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{first.CaseId}/documents"))).EnumerateArray());
        Assert.Empty((await OnFileAsync(claimant, second.CaseId)).GetProperty("documents").EnumerateArray());
        var slot = (await MockedApiFactory.ReadJsonAsync(use)).GetProperty("requiredDocuments").EnumerateArray().Single(row => row.GetProperty("documentTypeKey").GetString() == "CustomerId");
        Assert.True(slot.GetProperty("uploaded").GetBoolean());
    }

    [Fact]
    public async Task ADocumentCannotBeReusedWithoutAgreeingToThePrivacyNoticeOrForAKindThatWasNotOffered()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var phone = NewPhone();
        var first = await CreateClaimAsync(staff, tenant, phone);
        using (var claimantA = await TestData.ClaimantClientAsync(factory, staff, first))
        {
            await UploadAsync(claimantA, first.CaseId, "CustomerId", "id.jpg", new byte[1500], "image/jpeg");
            await UploadAsync(claimantA, first.CaseId, "PhotoEvidence", "photo.jpg", new byte[1500], "image/jpeg");
        }

        var second = await CreateClaimAsync(staff, tenant, phone);
        using var withoutConsent = await TestData.ClaimantClientAsync(factory, staff, second, agreeToPrivacyNotice: false);
        var offeredId = Assert.Single((await OnFileAsync(withoutConsent, second.CaseId)).GetProperty("documents").EnumerateArray()).GetProperty("documentId").GetGuid();
        var blocked = await withoutConsent.PostAsJsonAsync($"/api/v1/client-portal/claims/{second.CaseId}/on-file/use-document", new { sourceDocumentId = offeredId, documentTypeKey = "CustomerId" });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        using var agreed = await TestData.ClaimantClientAsync(factory, staff, second);
        // Photos of the damage are evidence of what happened to this claim, never offered for reuse.
        var photoId = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{first.CaseId}/documents"))).EnumerateArray().Single(document => document.GetProperty("originalFileName").GetString() == "photo.jpg").GetProperty("documentId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await agreed.PostAsJsonAsync($"/api/v1/client-portal/claims/{second.CaseId}/on-file/use-document", new { sourceDocumentId = photoId, documentTypeKey = "PhotoEvidence" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agreed.PostAsJsonAsync($"/api/v1/client-portal/claims/{second.CaseId}/on-file/use-document", new { sourceDocumentId = Guid.NewGuid(), documentTypeKey = "CustomerId" })).StatusCode);
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{second.CaseId}/documents"))).EnumerateArray());
    }

    [Fact]
    public async Task AnotherCustomerInTheSameWorkspaceNeverSeesSomeoneElsesDetailsOrDocuments()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var first = await CreateClaimAsync(staff, tenant, NewPhone());
        using (var claimantA = await TestData.ClaimantClientAsync(factory, staff, first))
        {
            await SaveDetailsAsync(claimantA, first.CaseId, new() { ["drivingPermitNumber"] = "DP-1" });
            await UploadAsync(claimantA, first.CaseId, "CustomerId", "id.jpg", new byte[1500], "image/jpeg");
        }

        var stranger = await CreateClaimAsync(staff, tenant, NewPhone());
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, stranger);

        var offered = await OnFileAsync(claimant, stranger.CaseId);
        Assert.Empty(offered.GetProperty("values").EnumerateArray());
        Assert.Empty(offered.GetProperty("documents").EnumerateArray());
    }

    [Fact]
    public async Task AnotherWorkspaceWithTheSamePhoneNumberNeverSeesTheseDetailsOrDocuments()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var staffA = factory.CreateScopedClient(tenantA);
        using var staffB = factory.CreateScopedClient(tenantB);
        var phone = NewPhone();
        var first = await CreateClaimAsync(staffA, tenantA, phone);
        using (var claimantA = await TestData.ClaimantClientAsync(factory, staffA, first))
        {
            await SaveDetailsAsync(claimantA, first.CaseId, new() { ["drivingPermitNumber"] = "DP-1" });
            await UploadAsync(claimantA, first.CaseId, "CustomerId", "id.jpg", new byte[1500], "image/jpeg");
        }

        var other = await CreateClaimAsync(staffB, tenantB, phone);
        using var claimantB = await TestData.ClaimantClientAsync(factory, staffB, other);

        var offered = await OnFileAsync(claimantB, other.CaseId);
        Assert.Empty(offered.GetProperty("values").EnumerateArray());
        Assert.Empty(offered.GetProperty("documents").EnumerateArray());
        // And one claimant cannot ask for another claim's offers.
        Assert.Equal(HttpStatusCode.Unauthorized, (await claimantB.GetAsync($"/api/v1/client-portal/claims/{first.CaseId}/on-file")).StatusCode);
    }

    [Fact]
    public async Task TheWorkspaceCanSwitchMemoryOffOrChooseWhichDetailsAndDocumentsAreWorthReusing()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var phone = NewPhone();
        var first = await CreateClaimAsync(staff, tenant, phone);
        using (var claimantA = await TestData.ClaimantClientAsync(factory, staff, first))
        {
            await SaveDetailsAsync(claimantA, first.CaseId, new() { ["drivingPermitNumber"] = "DP-1", ["vehicleRegistration"] = "UAX 123A" });
        }

        var second = await CreateClaimAsync(staff, tenant, phone);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, second);
        Assert.Equal(2, (await OnFileAsync(claimant, second.CaseId)).GetProperty("values").GetArrayLength());

        Assert.True((await staff.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { customerMemoryFieldKeys = new[] { "vehicleRegistration" } })).IsSuccessStatusCode);
        var narrowed = (await OnFileAsync(claimant, second.CaseId)).GetProperty("values").EnumerateArray().Select(value => value.GetProperty("key").GetString()).ToArray();
        Assert.Equal(new[] { "vehicleRegistration" }, narrowed);

        Assert.True((await staff.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { customerMemoryEnabled = false })).IsSuccessStatusCode);
        Assert.Empty((await OnFileAsync(claimant, second.CaseId)).GetProperty("values").EnumerateArray());
        var settings = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/admin/tenant-settings"));
        Assert.False(settings.GetProperty("customerMemoryEnabled").GetBoolean());
        Assert.Contains("drivingPermitNumber", settings.GetProperty("defaultCustomerMemoryFieldKeys").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task BadKeysAreRefusedWhenSavingTheMemorySettings()
    {
        using var staff = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { customerMemoryFieldKeys = new[] { "has space" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { customerMemoryDocumentKeys = new[] { new string('a', 61) } })).StatusCode);
    }

    [Fact]
    public async Task OnlyAClaimThatBelongsToThisCustomerAndNotANewClaimLinkCanAskWhatIsOnFile()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await CreateClaimAsync(staff, tenant, NewPhone());
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", tenant);
        anonymous.DefaultRequestHeaders.Add("X-Workspace-Id", "workspace-main");

        // In a developer runtime a request with no session falls back to the header identity, which owns nothing.
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}/on-file")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/client-portal/known-details?productLine=Motor")).StatusCode);
        // A link for an existing claim is not a link for starting a new one.
        Assert.Equal(HttpStatusCode.Conflict, (await claimant.GetAsync("/api/v1/client-portal/known-details?productLine=Motor")).StatusCode);
    }
}
