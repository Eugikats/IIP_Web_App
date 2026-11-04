using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 14: every file is looked at against the workspace's whole document
/// catalogue and named; the customer's slot wins; an unslotted file goes to the matching slot when
/// the recogniser is sure and to a person otherwise.</summary>
public sealed partial class DocumentRecognitionApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string Stage = "acknowledgement_and_document_request";
    private static readonly object[] ClaimTypes = [new { name = "Own damage", active = true }];

    private static object Doc(string key, string label, bool multiple = false) => new
    {
        documentKey = key,
        customerLabel = label,
        reason = "Needed to assess the claim.",
        isRequired = true,
        requiredAtStageKey = Stage,
        isRegulatoryOrWordingMandated = false,
        claimTypeKeys = Array.Empty<string>(),
        allowsMultiplePages = multiple,
        maxFiles = multiple ? 5 : (int?)null,
        acceptedFormats = Array.Empty<string>(),
    };

    private async Task<(string Tenant, HttpClient Staff, CaseInfo Claim)> ArrangeAsync(WebApplicationFactoryHolder? host = null, params object[] matrix)
    {
        var tenant = MockedApiFactory.NewTenant();
        var staff = (host?.Factory ?? factory).CreateScopedClient(tenant);
        // Custom document types must exist before a product can use them.
        await TestData.PublishProductAsync(host?.Factory ?? factory, tenant, claimTypes: ClaimTypes, documentMatrix: matrix.Length > 0 ? matrix : [Doc("PoliceAbstract", "Police report"), Doc("ClaimForm", "Claim form")]);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        return (tenant, staff, claim);
    }

    private static async Task<JsonElement> StaffUploadAsync(HttpClient staff, Guid caseId, string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", fileName);
        var response = await staff.PostAsync($"/api/v1/cases/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var documents = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/documents"));
        return documents.EnumerateArray().First(document => document.GetProperty("originalFileName").GetString() == fileName);
    }

    private static async Task<JsonElement> ClaimantUploadAsync(HttpClient claimant, Guid caseId, string slotKey, string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", fileName);
        form.Add(new StringContent(slotKey), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await MockedApiFactory.ReadJsonAsync(response);
    }

    [GeneratedRegex(@"^[A-Za-z0-9]+_CLM-[0-9A-F]{8}(_\d+)?\.pdf$")]
    private static partial Regex NamePattern();

    [Fact]
    public async Task TheCustomersSlotWins_ConfidentDisagreementIsAStaffFlagAndNeverAnErrorForTheCustomer()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var response = await ClaimantUploadAsync(claimant, claim.CaseId, "PoliceAbstract", "scan.pdf", "IIP-MOCK-DOC:ClaimForm");

        var row = response.GetProperty("requiredDocuments").EnumerateArray().Single(candidate => candidate.GetProperty("documentTypeKey").GetString() == "PoliceAbstract");
        Assert.Equal("Received", row.GetProperty("status").GetString());
        Assert.DoesNotContain("reads as", response.GetRawText());
        var document = await StaffDocumentAsync(staff, claim.CaseId, "scan.pdf");
        Assert.True(document.GetProperty("classifierDisagrees").GetBoolean());
        Assert.Equal("ClaimForm", document.GetProperty("recognisedTypeKey").GetString());
        Assert.Equal("PoliceAbstract", document.GetProperty("customerDocumentTypeKey").GetString());
        Assert.False(document.GetProperty("needsReview").GetBoolean());
        Assert.Contains(document.GetProperty("validationWarnings").EnumerateArray(), warning => warning.GetProperty("code").GetString() == "document.classifier.disagrees");
        Assert.StartsWith("PoliceReport_CLM-", document.GetProperty("displayName").GetString());
    }

    private static async Task SaveCustomTypeAsync(HttpClient staff, string key, string label, string description)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/document-types/{key}")
        {
            Content = JsonContent.Create(new { customerLabel = label, description, active = true }),
        };
        request.Headers.Add("X-Audit-Reason", "Added for the recognition tests");
        var saved = await staff.SendAsync(request);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> StaffDocumentAsync(HttpClient staff, Guid caseId, string fileName) =>
        (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/documents")))
            .EnumerateArray().First(document => document.GetProperty("originalFileName").GetString() == fileName);

    [Fact]
    public async Task EveryFileIsNamedByItsTypeAndClaim_AndTheOriginalNameIsKept()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        var document = await StaffUploadAsync(staff, claim.CaseId, "IMG_2031.pdf", "IIP-MOCK-DOC:PoliceAbstract");

        Assert.Equal("IMG_2031.pdf", document.GetProperty("originalFileName").GetString());
        var displayName = document.GetProperty("displayName").GetString()!;
        Assert.Matches(NamePattern(), displayName);
        Assert.StartsWith($"PoliceReport_CLM-{claim.CaseId.ToString("N")[..8].ToUpperInvariant()}", displayName);
        Assert.EndsWith(".pdf", displayName);
    }

    [Fact]
    public async Task AFileWithNoSlotIsFiledIntoTheMatchingSlotWhenTheRecogniserIsSure()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var document = await StaffUploadAsync(staff, claim.CaseId, "scan.pdf", "IIP-MOCK-DOC:PoliceAbstract");

        Assert.True(document.GetProperty("filedByRecognition").GetBoolean());
        Assert.Equal("PoliceAbstract", document.GetProperty("customerDocumentTypeKey").GetString());
        Assert.False(document.GetProperty("needsReview").GetBoolean());
        var customerView = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"));
        var row = customerView.GetProperty("requiredDocuments").EnumerateArray().Single(candidate => candidate.GetProperty("documentTypeKey").GetString() == "PoliceAbstract");
        Assert.Equal("Received", row.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AFileTheRecogniserIsNotSureAboutGoesToStaffReviewAndIsNotFiled()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        var document = await StaffUploadAsync(staff, claim.CaseId, "blurry.pdf", "IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-CONFIDENCE:0.45");

        Assert.True(document.GetProperty("needsReview").GetBoolean());
        Assert.Contains("could not tell", document.GetProperty("reviewReason").GetString());
        Assert.False(document.GetProperty("filedByRecognition").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("customerDocumentTypeKey").ValueKind);
        Assert.StartsWith("Unsorted_CLM-", document.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ASureRecognitionOfSomethingTheClaimDoesNotAskForGoesToReviewWithTheReason()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        var document = await StaffUploadAsync(staff, claim.CaseId, "report.pdf", "IIP-MOCK-DOC:MedicalReport");

        Assert.True(document.GetProperty("needsReview").GetBoolean());
        Assert.Contains("does not ask for", document.GetProperty("reviewReason").GetString());
        Assert.StartsWith("MedicalReport_CLM-", document.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ASecondFileOfTheSameKindGetsANumber_AndAFilledSingleFileSlotSendsTheNextToReview()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        var first = await StaffUploadAsync(staff, claim.CaseId, "a.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        var second = await StaffUploadAsync(staff, claim.CaseId, "b.pdf", "IIP-MOCK-DOC:PoliceAbstract");

        Assert.False(first.GetProperty("needsReview").GetBoolean());
        Assert.True(second.GetProperty("needsReview").GetBoolean());
        Assert.Contains("already in place", second.GetProperty("reviewReason").GetString());
        Assert.DoesNotMatch(@"_\d+\.pdf$", first.GetProperty("displayName").GetString()!);
        Assert.EndsWith("_2.pdf", second.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ACustomDocumentTypeTheInsurerAddedIsRecognisedAndFiled()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        await SaveCustomTypeAsync(staff, "LogBook", "Vehicle log book", "The registration document of the vehicle.");
        await TestData.PublishProductAsync(factory, tenant, claimTypes: ClaimTypes, documentMatrix: [Doc("LogBook", "Vehicle log book"), Doc("ClaimForm", "Claim form")]);
        var claim = await TestData.CreateCaseAsync(staff, tenant);

        var document = await StaffUploadAsync(staff, claim.CaseId, "photo_of_book.pdf", "IIP-MOCK-DOC:LogBook");

        Assert.True(document.GetProperty("filedByRecognition").GetBoolean());
        Assert.Equal("LogBook", document.GetProperty("customerDocumentTypeKey").GetString());
        Assert.StartsWith("VehicleLogBook_CLM-", document.GetProperty("displayName").GetString());
        Assert.Contains("vehicle log book", document.GetProperty("recognitionReason").GetString());
    }

    [Fact]
    public async Task AnotherWorkspacesCustomTypeIsNotRecognisedHere()
    {
        var owner = MockedApiFactory.NewTenant();
        using (var ownerStaff = factory.CreateScopedClient(owner))
        {
            await SaveCustomTypeAsync(ownerStaff, "LogBook", "Vehicle log book", "d");
        }

        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;

        var document = await StaffUploadAsync(staff, claim.CaseId, "book.pdf", "IIP-MOCK-DOC:LogBook");

        Assert.True(document.GetProperty("needsReview").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("recognisedTypeKey").ValueKind);
    }

    [Fact]
    public async Task APersonCanFileAReviewDocumentIntoASlot_AndCompletenessFollows()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        var unsorted = await StaffUploadAsync(staff, claim.CaseId, "mystery.pdf", "nothing recognisable");
        Assert.True(unsorted.GetProperty("needsReview").GetBoolean());

        var filed = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/documents/{unsorted.GetProperty("documentId").GetGuid()}/file-into", new { documentTypeKey = "ClaimForm" });

        Assert.Equal(HttpStatusCode.OK, filed.StatusCode);
        var result = await MockedApiFactory.ReadJsonAsync(filed);
        Assert.False(result.GetProperty("needsReview").GetBoolean());
        Assert.Equal("ClaimForm", result.GetProperty("customerDocumentTypeKey").GetString());
        Assert.StartsWith("ClaimForm_CLM-", result.GetProperty("displayName").GetString());
        Assert.Equal("mystery.pdf", result.GetProperty("originalFileName").GetString());
        var checklist = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/document-checklist"));
        Assert.True(checklist.GetProperty("completenessScore").GetDecimal() > 0);
        Assert.Contains("document.filed", await staff.GetStringAsync("/api/v1/audit/events"));
    }

    [Fact]
    public async Task FilingIntoASlotTheClaimDoesNotHaveOrAFilledOneIsRefused()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        await StaffUploadAsync(staff, claim.CaseId, "a.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        var other = await StaffUploadAsync(staff, claim.CaseId, "b.pdf", "nothing");
        var url = $"/api/v1/cases/{claim.CaseId}/documents/{other.GetProperty("documentId").GetGuid()}/file-into";

        var noSuchSlot = await staff.PostAsJsonAsync(url, new { documentTypeKey = "MedicalReport" });
        var filled = await staff.PostAsJsonAsync(url, new { documentTypeKey = "PoliceAbstract" });
        var empty = await staff.PostAsJsonAsync(url, new { documentTypeKey = "" });

        Assert.Equal(HttpStatusCode.BadRequest, noSuchSlot.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, filled.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task FilingIsLimitedToTheWorkspaceAndToStaffWhoMayUpload()
    {
        var (tenant, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        var unsorted = await StaffUploadAsync(staff, claim.CaseId, "mystery.pdf", "nothing");
        var url = $"/api/v1/cases/{claim.CaseId}/documents/{unsorted.GetProperty("documentId").GetGuid()}/file-into";
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var broker = factory.CreateScopedClient(tenant, role: "Compliance");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(url, new { documentTypeKey = "ClaimForm" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await broker.PostAsJsonAsync(url, new { documentTypeKey = "ClaimForm" })).StatusCode);
    }

    [Fact]
    public async Task APersonsReclassificationSettlesWhatItIsAndClearsTheReviewFlag()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        var unsorted = await StaffUploadAsync(staff, claim.CaseId, "mystery.pdf", "nothing");

        var reclassified = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/documents/{unsorted.GetProperty("documentId").GetGuid()}/reclassify", new { documentType = "Invoice" });

        Assert.Equal(HttpStatusCode.OK, reclassified.StatusCode);
        var document = await StaffDocumentAsync(staff, claim.CaseId, "mystery.pdf");
        Assert.False(document.GetProperty("needsReview").GetBoolean());
        Assert.Equal("Invoice", document.GetProperty("recognisedTypeKey").GetString());
        Assert.Equal("Set by a person.", document.GetProperty("recognitionReason").GetString());
    }

    [Fact]
    public async Task ARecogniserThatFailsNeverBlocksTheClaim_TheFileJustGoesToAPerson()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IDocumentRecognizer>(new ThrowingRecognizer()))));
        var tenant = MockedApiFactory.NewTenant();
        using var staff = host.CreateScopedClient(tenant);
        await TestData.PublishProductAsync(host, tenant, claimTypes: ClaimTypes, documentMatrix: [Doc("PoliceAbstract", "Police report")]);
        var claim = await TestData.CreateCaseAsync(staff, tenant);

        var document = await StaffUploadAsync(staff, claim.CaseId, "a.pdf", "IIP-MOCK-DOC:PoliceAbstract");

        Assert.Equal("Processed", document.GetProperty("processingStatus").GetString());
        Assert.True(document.GetProperty("needsReview").GetBoolean());
        Assert.Contains("could not be looked at", document.GetProperty("recognitionReason").GetString());
    }

    [Fact]
    public async Task TheClaimantsOwnViewNeverCarriesTheRecognitionOrTheDisplayName()
    {
        var (_, staff, claim) = await ArrangeAsync();
        using var _s = staff;
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var response = await ClaimantUploadAsync(claimant, claim.CaseId, "PoliceAbstract", "scan.pdf", "IIP-MOCK-DOC:PoliceAbstract");

        var text = response.GetRawText();
        Assert.DoesNotContain("recognis", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("displayName", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("needsReview", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingRecognizer : IDocumentRecognizer
    {
        public Task<DocumentRecognition> RecognizeAsync(DocumentRecognitionRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the model is down");
    }

    public sealed record WebApplicationFactoryHolder(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory);
}
