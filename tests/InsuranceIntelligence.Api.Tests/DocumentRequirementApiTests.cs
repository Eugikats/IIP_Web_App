using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 4 at HTTP level: documents and evidence per claim type, with limits.</summary>
public sealed class DocumentRequirementApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string Stage = "acknowledgement_and_document_request";

    private static readonly object[] ClaimTypes =
    [
        new { name = "Own damage", active = true },
        new { name = "Theft", active = true },
    ];

    private static object Doc(string key, string label, bool required, string[]? claimTypes = null, string? kind = null, int? maxFiles = null, string[]? formats = null, bool multiple = false) =>
        new
        {
            documentKey = key,
            customerLabel = label,
            reason = "Needed to assess the claim.",
            isRequired = required,
            requiredAtStageKey = Stage,
            isRegulatoryOrWordingMandated = false,
            claimTypeKeys = claimTypes ?? [],
            kind,
            allowsMultiplePages = multiple,
            maxFiles,
            acceptedFormats = formats ?? [],
        };

    private async Task<(string Tenant, HttpClient Staff)> SetUpAsync(params object[] matrix)
    {
        var tenant = MockedApiFactory.NewTenant();
        await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes, documentMatrix: matrix);
        return (tenant, factory.CreateScopedClient(tenant));
    }

    private static async Task<string[]> SlotKeysAsync(HttpClient staff, Guid caseId)
    {
        var checklist = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/document-checklist"));
        return checklist.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("key").GetString()!).ToArray();
    }

    [Fact]
    public async Task EachClaimTypeGetsItsOwnDocuments()
    {
        var (tenant, staff) = await SetUpAsync(
            Doc("ClaimForm", "Claim form", true),
            Doc("PoliceAbstract", "Police report", true, ["theft"]),
            Doc("GarageEstimate", "Garage estimate", false, ["own-damage"]),
            Doc("PhotoEvidence", "Photos", true));
        using var _ = staff;

        var theft = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        var damage = await TestData.CreateCaseAsync(staff, tenant, claimType: "Own damage");

        Assert.Equal(new[] { "ClaimForm", "PoliceAbstract", "PhotoEvidence" }, await SlotKeysAsync(staff, theft.CaseId));
        Assert.Equal(new[] { "ClaimForm", "PhotoEvidence", "GarageEstimate" }, await SlotKeysAsync(staff, damage.CaseId));
    }

    [Fact]
    public async Task Completeness_IsMeasuredAgainstTheClaimTypesOwnRequiredDocuments()
    {
        var (tenant, staff) = await SetUpAsync(
            Doc("ClaimForm", "Claim form", true),
            Doc("PoliceAbstract", "Police report", true, ["theft"]));
        using var _ = staff;
        var theft = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        var damage = await TestData.CreateCaseAsync(staff, tenant, claimType: "Own damage");

        var theftChecklist = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{theft.CaseId}/document-checklist"));
        var damageChecklist = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{damage.CaseId}/document-checklist"));

        Assert.Equal(2, theftChecklist.GetProperty("requiredTotal").GetInt32());
        Assert.Equal(1, damageChecklist.GetProperty("requiredTotal").GetInt32());
    }

    [Fact]
    public async Task TheCustomerSeesKindLimitsAndFormats_AndEvidenceIsMarkedAsEvidence()
    {
        var (tenant, staff) = await SetUpAsync(
            Doc("PoliceAbstract", "Police report", true),
            Doc("PhotoEvidence", "Photos of the damage", true, maxFiles: 5, formats: ["jpg", "png"]));
        using var _ = staff;
        var claim = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var view = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"));

        var slots = view.GetProperty("requiredDocuments").EnumerateArray().ToDictionary(slot => slot.GetProperty("documentTypeKey").GetString()!);
        Assert.Equal("Document", slots["PoliceAbstract"].GetProperty("kind").GetString());
        Assert.False(slots["PoliceAbstract"].GetProperty("allowsMultipleFiles").GetBoolean());
        Assert.Equal("Evidence", slots["PhotoEvidence"].GetProperty("kind").GetString());
        Assert.True(slots["PhotoEvidence"].GetProperty("allowsMultipleFiles").GetBoolean());
        Assert.Equal(5, slots["PhotoEvidence"].GetProperty("maxFiles").GetInt32());
        Assert.Equal(new[] { "jpg", "png" }, slots["PhotoEvidence"].GetProperty("acceptedFormats").EnumerateArray().Select(format => format.GetString()));
        // The tenant's own wording is what the customer reads.
        Assert.Equal("Photos of the damage", slots["PhotoEvidence"].GetProperty("label").GetString());
    }

    [Fact]
    public async Task AnEvidenceSlot_TakesSeveralPhotos_UpToItsLimit_AndKeepsThemAll()
    {
        var (tenant, staff) = await SetUpAsync(Doc("PhotoEvidence", "Photos of the damage", true, maxFiles: 4, formats: ["jpg"]));
        using var _ = staff;
        var claim = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var first = await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "a1.jpg", "a2.jpg", "a3.jpg");
        var tooMany = await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "b1.jpg", "b2.jpg");
        var fits = await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "b1.jpg");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Contains("1 more", await tooMany.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, fits.StatusCode);
        var slot = (await MockedApiFactory.ReadJsonAsync(fits)).GetProperty("requiredDocuments").EnumerateArray().Single();
        Assert.Equal(4, slot.GetProperty("fileCount").GetInt32());
        Assert.Equal(4, slot.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public async Task ASingleFileSlot_IsReplacedByANewUpload_ButAMultiFileSlotIsNot()
    {
        var (tenant, staff) = await SetUpAsync(Doc("PoliceAbstract", "Police report", true));
        using var _ = staff;
        var claim = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "one.pdf");
        var second = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "two.pdf");
        var twoAtOnce = await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "x.pdf", "y.pdf");

        var slot = (await MockedApiFactory.ReadJsonAsync(second)).GetProperty("requiredDocuments").EnumerateArray().Single();
        Assert.Equal(1, slot.GetProperty("fileCount").GetInt32());
        Assert.Equal("two.pdf", slot.GetProperty("fileName").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, twoAtOnce.StatusCode);
    }

    [Fact]
    public async Task AFileTypeTheSlotDoesNotAccept_IsRefusedInPlainWords()
    {
        var (tenant, staff) = await SetUpAsync(Doc("PhotoEvidence", "Photos", true, formats: ["jpg", "png"]));
        using var _ = staff;
        var claim = await TestData.CreateCaseAsync(staff, tenant, claimType: "Theft");
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var response = await UploadAsync(claimant, claim.CaseId, "PhotoEvidence", "report.pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("JPG, PNG", text);
        Assert.Contains("report.pdf", text);
    }

    [Fact]
    public async Task AnUploadIntoASlotTheClaimDoesNotHave_IsRefused()
    {
        var (tenant, staff) = await SetUpAsync(Doc("PoliceAbstract", "Police report", true, ["theft"]));
        using var _ = staff;
        var damage = await TestData.CreateCaseAsync(staff, tenant, claimType: "Own damage");
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, damage);

        var response = await UploadAsync(claimant, damage.CaseId, "PoliceAbstract", "police.pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("unknown-claim-type")]
    [InlineData("duplicate-all")]
    [InlineData("duplicate-specific")]
    [InlineData("bad-kind")]
    [InlineData("bad-format")]
    [InlineData("too-many-files")]
    public async Task InvalidDocumentRequirements_AreRefused(string problem)
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes);
        using var admin = factory.CreateScopedClient(tenant);
        object[] matrix = problem switch
        {
            "unknown-claim-type" => [Doc("ClaimForm", "Form", true, ["marine"])],
            "duplicate-all" => [Doc("ClaimForm", "Form", true), Doc("ClaimForm", "Form again", true)],
            "duplicate-specific" => [Doc("PoliceAbstract", "A", true, ["theft"]), Doc("PoliceAbstract", "B", true, ["theft"])],
            "bad-kind" => [Doc("ClaimForm", "Form", true, kind: "Selfie")],
            "bad-format" => [Doc("ClaimForm", "Form", true, formats: ["exe"])],
            _ => [Doc("PhotoEvidence", "Photos", true, maxFiles: 500)],
        };

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new { changeReason = "bad", documentMatrix = matrix });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("documentMatrix", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ADocumentKeyMayRepeatWhenEachEntryNamesDifferentClaimTypes()
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes);
        using var admin = factory.CreateScopedClient(tenant);

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new
        {
            changeReason = "per type",
            documentMatrix = new[]
            {
                Doc("PoliceAbstract", "Police report", true, ["theft"]),
                Doc("PoliceAbstract", "Police report if anyone was hurt", false, ["own-damage"]),
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AClaimTypeStillUsedByADocument_CannotBeRemoved()
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes, documentMatrix: [Doc("PoliceAbstract", "Police report", true, ["theft"])]);
        using var admin = factory.CreateScopedClient(tenant);

        var response = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new
        {
            changeReason = "drop theft",
            claimTypes = new object[] { new { name = "Own damage", active = true } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PoliceAbstract", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheProductReportsTheDefaultsItAppliedToEachRequirement()
    {
        var tenant = MockedApiFactory.NewTenant();
        var productId = await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes, documentMatrix: [Doc("PhotoEvidence", "Photos", true), Doc("ClaimForm", "Form", true, multiple: true)]);
        using var admin = factory.CreateScopedClient(tenant);

        var product = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));

        var entries = product.GetProperty("documentMatrix").EnumerateArray().ToDictionary(entry => entry.GetProperty("documentKey").GetString()!);
        Assert.Equal("Evidence", entries["PhotoEvidence"].GetProperty("kind").GetString());
        Assert.Equal(10, entries["PhotoEvidence"].GetProperty("maxFiles").GetInt32());
        Assert.Equal("Document", entries["ClaimForm"].GetProperty("kind").GetString());
        Assert.Equal(10, entries["ClaimForm"].GetProperty("maxFiles").GetInt32());
        Assert.True(entries["ClaimForm"].GetProperty("allowsMultiplePages").GetBoolean());
    }

    [Fact]
    public async Task ThePreviewShowsTheSlotsForTheChosenClaimType()
    {
        var (_, staff) = await SetUpAsync(
            Doc("ClaimForm", "Claim form", true),
            Doc("PoliceAbstract", "Police report", true, ["theft"]));
        using var __ = staff;

        var theft = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/document-checklist/preview?productLine=Motor&claimType=Theft"));
        var damage = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/document-checklist/preview?productLine=Motor&claimType=Own%20damage"));

        Assert.Equal("theft", theft.GetProperty("claimTypeKey").GetString());
        Assert.Equal(new[] { "ClaimForm", "PoliceAbstract" }, theft.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("key").GetString()));
        Assert.Equal(new[] { "ClaimForm" }, damage.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("key").GetString()));
    }

    [Fact]
    public async Task AClaimOnAProductWithNoMatrix_StillGetsTheBuiltInRules_WithPhotosAsEvidence()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);

        var checklist = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/document-checklist"));

        var photos = checklist.GetProperty("slots").EnumerateArray().Single(slot => slot.GetProperty("key").GetString() == "PhotoEvidence");
        Assert.Equal("Evidence", photos.GetProperty("kind").GetString());
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient claimant, Guid caseId, string slotKey, params string[] fileNames)
    {
        using var form = new MultipartFormDataContent();
        foreach (var name in fileNames)
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes($"content of {name}"));
            file.Headers.ContentType = new MediaTypeHeaderValue(name.EndsWith(".pdf") ? "application/pdf" : "image/jpeg");
            form.Add(file, "files", name);
        }

        form.Add(new StringContent(slotKey), "documentTypeKey");
        return await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
    }
}
