using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Local.Mock;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 2 at HTTP level: failed processing is retryable and visible, and one
/// completeness rule applies whatever the classifier thinks.</summary>
public sealed class KnownDefectsApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static JsonElement Slot(JsonElement claim, string key) =>
        claim.GetProperty("requiredDocuments").EnumerateArray().Single(slot => slot.GetProperty("documentTypeKey").GetString() == key);

    [Fact]
    public async Task CustomerSlot_CountsForCompleteness_EvenWhenTheFileIsUnrecognisable()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, info);

        using var form = TestData.FileForm("IMG_2031.pdf", "nothing recognisable in here", documentTypeKey: "PoliceAbstract", contentType: "application/pdf");
        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", form);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var claim = await MockedApiFactory.ReadJsonAsync(upload);
        Assert.Equal("Received", Slot(claim, "PoliceAbstract").GetProperty("status").GetString());
        // Motor requires three documents; one required slot is filled.
        Assert.Equal(33, (int)Math.Round(claim.GetProperty("completenessScore").GetDecimal()));
    }

    [Fact]
    public async Task ClassifierDisagreement_RaisesAStaffFlag_AndIsNeverAnErrorForTheCustomer()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, info);

        using var form = TestData.FileForm("scan.pdf", "IIP-MOCK-DOC:Invoice", documentTypeKey: "PoliceAbstract", contentType: "application/pdf");
        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", form);
        var claim = await MockedApiFactory.ReadJsonAsync(upload);

        // The customer's choice stands: the slot is received and not shown as wrong.
        Assert.Equal("Received", Slot(claim, "PoliceAbstract").GetProperty("status").GetString());
        Assert.DoesNotContain("disagree", claim.GetRawText().Replace("disagreesWithClassifier", string.Empty), StringComparison.OrdinalIgnoreCase);

        var documents = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{info.CaseId}/documents"));
        var document = Assert.Single(documents.EnumerateArray());
        Assert.True(document.GetProperty("classifierDisagrees").GetBoolean());
        Assert.Contains(document.GetProperty("validationWarnings").EnumerateArray(),
            warning => warning.GetProperty("code").GetString() == "document.classifier.disagrees");
    }

    [Fact]
    public async Task FailedProcessing_IsMarkedFailedWithRetry_ThenSucceedsOnRetry()
    {
        var tenant = MockedApiFactory.NewTenant();
        var toggle = new ToggleDocumentProvider { Fail = true };
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IDocumentProcessingProvider>(toggle))));
        using var staff = host.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(host, staff, info);

        using var form = TestData.FileForm("police.pdf", "IIP-MOCK-DOC:PoliceAbstract", documentTypeKey: "PoliceAbstract", contentType: "application/pdf");
        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents", form);

        // The customer is not shown a server error for something they cannot fix.
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var failedClaim = await MockedApiFactory.ReadJsonAsync(upload);
        var slot = Slot(failedClaim, "PoliceAbstract");
        Assert.Equal("Could not be read", slot.GetProperty("status").GetString());
        Assert.True(slot.GetProperty("canRetry").GetBoolean());
        // A failed file does not count towards completeness.
        Assert.Equal(0, (int)failedClaim.GetProperty("completenessScore").GetDecimal());

        var documents = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{info.CaseId}/documents"));
        Assert.Equal("Failed", Assert.Single(documents.EnumerateArray()).GetProperty("processingStatus").GetString());

        toggle.Fail = false;
        var retry = await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents/retry", new { documentTypeKey = "PoliceAbstract" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retried = await MockedApiFactory.ReadJsonAsync(retry);
        Assert.Equal("Received", Slot(retried, "PoliceAbstract").GetProperty("status").GetString());
        Assert.False(Slot(retried, "PoliceAbstract").GetProperty("canRetry").GetBoolean());
        Assert.Equal(33, (int)Math.Round(retried.GetProperty("completenessScore").GetDecimal()));
    }

    [Fact]
    public async Task Retry_WithNothingFailed_IsRefused()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, info);

        var retry = await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{info.CaseId}/documents/retry", new { documentTypeKey = "PoliceAbstract" });

        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
    }

    [Fact]
    public async Task StaffUpload_WithNoProvider_IsHonest503_AndTheFileIsMarkedFailedThenReprocessable()
    {
        var tenant = MockedApiFactory.NewTenant();
        await using var broken = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IDocumentProcessingProvider, NotConfiguredDocumentProcessingProvider>())));
        using var brokenStaff = broken.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(brokenStaff, tenant);

        using var form = TestData.FileForm("police.txt", "IIP-MOCK-DOC:PoliceAbstract");
        var upload = await brokenStaff.PostAsync($"/api/v1/cases/{info.CaseId}/documents", form);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode);
        Assert.Equal("DocumentProcessing", (await MockedApiFactory.ReadJsonAsync(upload)).GetProperty("processingCapability").GetString());

        var documents = await MockedApiFactory.ReadJsonAsync(await brokenStaff.GetAsync($"/api/v1/cases/{info.CaseId}/documents"));
        var document = Assert.Single(documents.EnumerateArray());
        Assert.Equal("Failed", document.GetProperty("processingStatus").GetString());
        Assert.Contains(document.GetProperty("allowedActions").EnumerateArray(), action => action.GetProperty("action").GetString() == "reprocessDocument");

        // The provider comes back (the default Mock host): reprocessing the stored file succeeds.
        using var working = factory.CreateScopedClient(tenant);
        var reprocess = await working.PostAsync($"/api/v1/cases/{info.CaseId}/documents/{document.GetProperty("documentId").GetGuid()}/reprocess", null);
        Assert.Equal(HttpStatusCode.OK, reprocess.StatusCode);
        var processed = await MockedApiFactory.ReadJsonAsync(reprocess);
        Assert.Equal("Processed", processed.GetProperty("processingStatus").GetString());
        Assert.Equal("PoliceAbstract", processed.GetProperty("documentType").GetString());
    }

    [Fact]
    public async Task Reprocess_OfAnotherTenantsDocument_IsNotFound()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var staffA = factory.CreateScopedClient(tenantA);
        using var staffB = factory.CreateScopedClient(tenantB);
        var info = await TestData.CreateCaseAsync(staffA, tenantA);
        using var form = TestData.FileForm("police.txt", "IIP-MOCK-DOC:PoliceAbstract");
        await staffA.PostAsync($"/api/v1/cases/{info.CaseId}/documents", form);
        var documents = await MockedApiFactory.ReadJsonAsync(await staffA.GetAsync($"/api/v1/cases/{info.CaseId}/documents"));
        var documentId = documents.EnumerateArray().Single().GetProperty("documentId").GetGuid();

        var response = await staffB.PostAsync($"/api/v1/cases/{info.CaseId}/documents/{documentId}/reprocess", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomerServiceRole_CannotManageProducts()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var customerService = factory.CreateScopedClient(tenant, role: "CustomerService");

        var response = await customerService.PostAsJsonAsync("/api/v1/products", new { name = "Sneaky", productClass = "Motor" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class ToggleDocumentProvider : IDocumentProcessingProvider
    {
        private readonly MockDocumentProcessingProvider inner = new();
        public bool Fail { get; set; }

        public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default) =>
            Fail ? throw new InvalidOperationException("simulated provider failure") : inner.ProcessAsync(request, cancellationToken);
    }
}
