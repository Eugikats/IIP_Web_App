using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace InsuranceIntelligence.Api.Tests;

public sealed class DocumentEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DocumentExtractionResult_DeserializesLegacyPayloadWithoutFieldCorrections()
    {
        const string legacyJson = """
            {
              "fields": { "policyNumber": "POL-100" },
              "fieldConfidences": { "policyNumber": 0.91 },
              "providerName": "AzureDocumentIntelligence",
              "extractedAt": "2026-06-20T09:00:00Z",
              "approved": false,
              "approvedByUserId": null,
              "approvedAt": null
            }
            """;

        var extraction = JsonSerializer.Deserialize<DocumentExtractionResult>(legacyJson, JsonOptions);

        Assert.NotNull(extraction);
        Assert.Equal("POL-100", extraction.Fields["policyNumber"]);
        Assert.Null(extraction.FieldCorrections);
    }

    [Fact]
    public async Task UploadDocuments_UploadsAndProcessesDocumentForExistingCase()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var context = UploadContext("claim-form.pdf");

        var result = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);

        var (statusCode, response) = await ExecuteAsync<DocumentUploadResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var document = Assert.Single(response!.Documents);
        Assert.Equal("ClaimForm", document.DocumentType);
        Assert.Equal("Processed", document.ProcessingStatus);
        Assert.Contains(document.AllowedActions, action => action.Action == "approveDocumentExtraction");
        Assert.Contains(document.AllowedActions, action => action.Action == "reclassifyDocument");
        Assert.Contains(document.AllowedActions, action => action.Action == "correctDocumentExtraction");
        Assert.StartsWith($"/api/v1/cases/{claimCase.CaseId}/documents/{document.DocumentId}/content", document.PreviewUrl, StringComparison.Ordinal);
        Assert.NotEmpty(document.Fields);
        Assert.DoesNotContain(document.Fields, field => field.Name.Equals("objectKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("objectKey", JsonSerializer.Serialize(document), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(33.33m, response.Checklist.CompletenessScore);
    }

    [Fact]
    public async Task CorrectExtractionField_PersistsCorrectionAndAuditWithoutLoggingCorrectedValue()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;
        const string correctedValue = "verified-claim-form.pdf";
        const string auditReason = "Claims handler compared the extracted filename with the submitted evidence.";

        var result = await DocumentEndpoints.CorrectExtractionField(
            claimCase.CaseId,
            documentId,
            new CorrectExtractionFieldRequest("sourceFileName", correctedValue),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: auditReason),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<DocumentResponse>(result);
        var correctedField = Assert.Single(response!.Fields, field => field.Name == "sourceFileName");
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);
        var correctionAudit = Assert.Single(auditEvents, audit => audit.ActionName == "document.extraction.field_corrected");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("claim-form.pdf", correctedField.ExtractedValue);
        Assert.Equal(correctedValue, correctedField.CorrectedValue);
        Assert.Equal(correctedValue, correctedField.EffectiveValue);
        Assert.Equal("Corrected", correctedField.VerificationStatus);
        Assert.Equal("corr-doc-001", correctedField.CorrectionReference);
        Assert.Equal(auditReason, correctionAudit.AuditReason);
        Assert.DoesNotContain(correctedValue, JsonSerializer.Serialize(correctionAudit), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorrectExtractionField_RequiresAuditReason()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;

        var result = await DocumentEndpoints.CorrectExtractionField(
            claimCase.CaseId,
            documentId,
            new CorrectExtractionFieldRequest("sourceFileName", "verified.pdf"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task CorrectExtractionField_RejectsInternalStorageField()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;

        var result = await DocumentEndpoints.CorrectExtractionField(
            claimCase.CaseId,
            documentId,
            new CorrectExtractionFieldRequest("objectKey", "replacement-storage-reference"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Attempted internal field correction should be blocked."),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    [Fact]
    public async Task GetDocumentContent_StreamsTenantScopedStoredContent()
    {
        var storageProvider = new ReadableDocumentStorageProvider();
        var dependencies = CreateDependencies(storageProvider);
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;

        var result = await DocumentEndpoints.GetDocumentContent(
            claimCase.CaseId,
            documentId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, contentType, body) = await ExecuteContentAsync(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("application/pdf", contentType);
        Assert.Equal("synthetic test document", body);
        Assert.StartsWith("tenant-demo/workspace-claims/", storageProvider.LastReadObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDocumentContent_ReturnsNotFoundForWorkspaceMismatch()
    {
        var storageProvider = new ReadableDocumentStorageProvider();
        var dependencies = CreateDependencies(storageProvider);
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;

        var result = await DocumentEndpoints.GetDocumentContent(
            claimCase.CaseId,
            documentId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            ScopedContext(workspaceId: "wrong-workspace"),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task ListDocuments_ReturnsDocumentsForCase()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await UploadClaimFormAsync(dependencies, claimCase.CaseId);

        var result = await DocumentEndpoints.ListDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            ScopedContext(),
            CancellationToken.None);

        var (statusCode, response) = await ExecuteAsync<DocumentResponse[]>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Single(response!);
    }

    [Fact]
    public async Task GetChecklist_ReturnsChecklistForCase()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await UploadClaimFormAsync(dependencies, claimCase.CaseId);

        var result = await DocumentEndpoints.GetChecklist(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            ScopedContext(),
            CancellationToken.None);

        var (statusCode, response) = await ExecuteAsync<DocumentChecklistResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(3, response!.RequiredTotal);
        Assert.Contains(response.MissingRequiredDocuments, item => item.DocumentType == "PoliceAbstract");
    }

    [Fact]
    public async Task UploadDocuments_RejectsMissingTenantHeader()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var context = UploadContext("claim-form.pdf", tenantId: null);

        var result = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);

        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task UploadDocuments_RejectsMissingWorkspaceHeader()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var context = UploadContext("claim-form.pdf", workspaceId: null);

        var result = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);

        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task UploadDocuments_ReturnsNotFoundForTenantWorkspaceMismatch()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var context = UploadContext("claim-form.pdf", workspaceId: "wrong-workspace");

        var result = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);

        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task UploadDocuments_RejectsUnsupportedFile()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var context = UploadContext("claim-form.exe");

        var result = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);

        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task UploadDocuments_WritesDocumentAndCompletenessAuditEvents()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await UploadClaimFormAsync(dependencies, claimCase.CaseId);

        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "document.uploaded");
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "document.processed");
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "case.completeness.updated");
    }

    [Fact]
    public async Task UploadDocuments_ProvidesScopedStoredDocumentStreamToProcessingProvider()
    {
        var storageProvider = new ReadableDocumentStorageProvider();
        var processingProvider = new StreamReadingProcessingProvider();
        var dependencies = CreateDependencies(storageProvider, processingProvider);
        var claimCase = await CreateCaseAsync(dependencies);

        var result = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);

        var (statusCode, response) = await ExecuteAsync<DocumentUploadResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.NotNull(response);
        Assert.True(processingProvider.StreamWasRead);
        Assert.True(storageProvider.LastReadStream?.Disposed);
        Assert.StartsWith("tenant-demo/workspace-claims/", storageProvider.LastReadObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApproveExtraction_BlocksRepeatApproval()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;

        var approveResult = await DocumentEndpoints.ApproveExtraction(
            claimCase.CaseId,
            documentId,
            new ApproveExtractionRequest("user-claims-1"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (approveStatus, approved) = await ExecuteAsync<DocumentResponse>(approveResult);

        Assert.Equal(StatusCodes.Status200OK, approveStatus);
        Assert.True(approved!.ExtractionApproved);
        Assert.Contains(approved.BlockedActions, action => action.Action == "approveDocumentExtraction");
        Assert.Contains(approved.BlockedActions, action => action.Action == "correctDocumentExtraction");

        var repeatResult = await DocumentEndpoints.ApproveExtraction(
            claimCase.CaseId,
            documentId,
            new ApproveExtractionRequest("user-claims-1"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (repeatStatus, _) = await ExecuteAsync<JsonElement>(repeatResult);

        Assert.Equal(StatusCodes.Status409Conflict, repeatStatus);
    }

    [Fact]
    public async Task ReclassifyDocument_BlocksApprovedExtraction()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;

        var approveResult = await DocumentEndpoints.ApproveExtraction(
            claimCase.CaseId,
            documentId,
            new ApproveExtractionRequest("user-claims-1"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (approveStatus, _) = await ExecuteAsync<DocumentResponse>(approveResult);
        Assert.Equal(StatusCodes.Status200OK, approveStatus);

        var reclassifyResult = await DocumentEndpoints.ReclassifyDocument(
            claimCase.CaseId,
            documentId,
            new ReclassifyDocumentRequest("Invoice", "user-claims-1"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (reclassifyStatus, _) = await ExecuteAsync<JsonElement>(reclassifyResult);

        Assert.Equal(StatusCodes.Status409Conflict, reclassifyStatus);
    }

    [Fact]
    public async Task ReclassifyDocument_PersistsAuditReasonHeader()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var uploadResult = await DocumentEndpoints.UploadDocuments(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (_, uploadResponse) = await ExecuteAsync<DocumentUploadResponse>(uploadResult);
        var documentId = Assert.Single(uploadResponse!.Documents).DocumentId;

        const string auditReason = "Claims handler verified the submitted repair estimate before reclassification.";
        var reclassifyResult = await DocumentEndpoints.ReclassifyDocument(
            claimCase.CaseId,
            documentId,
            new ReclassifyDocumentRequest("Invoice", "user-claims-1"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: auditReason),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(reclassifyResult);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "document.reclassified"
            && auditEvent.AuditReason == auditReason);
    }

    private static async Task UploadClaimFormAsync(Dependencies dependencies, Guid caseId)
    {
        var result = await DocumentEndpoints.UploadDocuments(
            caseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("claim-form.pdf"),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<DocumentUploadResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    private static async Task<DocumentResponse> UploadAndReturnDocumentAsync(Dependencies dependencies, Guid caseId)
    {
        var result = await DocumentEndpoints.UploadDocuments(
            caseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.StorageProvider,
            dependencies.ProcessingProvider,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            UploadContext("repair-estimate.pdf"),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<DocumentUploadResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        return Assert.Single(response!.Documents);
    }

    private static async Task<ClaimCase> CreateCaseAsync(Dependencies dependencies)
    {
        var service = new CaseIntakeService(dependencies.CaseRepository, dependencies.AuditRepository);

        return await service.CreateCaseAsync(
            new CreateCaseCommand(
                InsuranceIntelligence.Domain.Common.TenantScope.Create("tenant-demo", "workspace-claims"),
                "Amina Okello",
                "amina@example.test",
                "+256700000000",
                "POL-1001",
                "Motor",
                "Accident",
                "WebPortal",
                new DateOnly(2026, 5, 24),
                "Minor collision reported at branch intake.",
                "user-claims-1",
                "corr-api-001"));
    }

    private static Dependencies CreateDependencies(
        IDocumentStorageProvider? storageProvider = null,
        IDocumentProcessingProvider? processingProvider = null)
    {
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var scoringService = new CompletenessScoringService();

        return new Dependencies(
            caseRepository,
            auditRepository,
            documentRepository,
            storageProvider ?? new StubDocumentStorageProvider(),
            processingProvider ?? new MockDocumentProcessingProvider(),
            new StaticDocumentChecklistProvider(scoringService));
    }

    private static DefaultHttpContext ScopedContext(
        string? tenantId = "tenant-demo",
        string? workspaceId = "workspace-claims",
        string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-doc-001" };
        if (tenantId is not null)
        {
            context.Request.Headers["X-Tenant-Id"] = tenantId;
        }

        if (workspaceId is not null)
        {
            context.Request.Headers["X-Workspace-Id"] = workspaceId;
        }

        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["X-Correlation-ID"] = "corr-doc-001";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }

        return context;
    }

    private static DefaultHttpContext UploadContext(
        string fileName,
        string? tenantId = "tenant-demo",
        string? workspaceId = "workspace-claims")
    {
        var context = ScopedContext(tenantId, workspaceId);
        var bytes = Encoding.UTF8.GetBytes("synthetic test document");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = ContentTypeFor(fileName)
        };
        var files = new FormFileCollection { file };
        var form = new FormCollection(
            new Dictionary<string, StringValues>
            {
                ["uploadedByUserId"] = "user-claims-1"
            },
            files);

        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = form;

        return context;
    }

    private static string ContentTypeFor(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }

    private static async Task<(int StatusCode, string? ContentType, string Body)> ExecuteContentAsync(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8, leaveOpen: true);
        return (httpContext.Response.StatusCode, httpContext.Response.ContentType, await reader.ReadToEndAsync());
    }

    private sealed record Dependencies(
        InMemoryCaseRepository CaseRepository,
        InMemoryAuditRepository AuditRepository,
        InMemoryCaseDocumentRepository DocumentRepository,
        IDocumentStorageProvider StorageProvider,
        IDocumentProcessingProvider ProcessingProvider,
        IDocumentChecklistProvider ChecklistProvider);

    private sealed class StubDocumentStorageProvider : IDocumentStorageProvider
    {
        public Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentStorageResult(
                $"{request.Scope.TenantId}/{request.Scope.WorkspaceId}/{request.CaseId}/{request.DocumentId}_{request.OriginalFileName}",
                request.ContentType,
                request.FileSizeBytes));
    }

    private sealed class ReadableDocumentStorageProvider : IDocumentStorageProvider
    {
        private readonly Dictionary<string, byte[]> storedDocuments = new(StringComparer.Ordinal);

        public TrackingReadStream? LastReadStream { get; private set; }

        public string? LastReadObjectKey { get; private set; }

        public async Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            var objectKey = $"{request.Scope.TenantId}/{request.Scope.WorkspaceId}/{request.CaseId}/{request.DocumentId}_{request.OriginalFileName}";
            storedDocuments[objectKey] = memory.ToArray();
            return new DocumentStorageResult(objectKey, request.ContentType, request.FileSizeBytes);
        }

        public Task<Stream> ReadAsync(InsuranceIntelligence.Domain.Common.TenantScope scope, string objectKey, CancellationToken cancellationToken = default)
        {
            var expectedPrefix = $"{scope.TenantId}/{scope.WorkspaceId}/";
            if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Document storage reference is outside the requested tenant workspace.");
            }

            LastReadObjectKey = objectKey;
            LastReadStream = new TrackingReadStream(storedDocuments[objectKey]);
            return Task.FromResult<Stream>(LastReadStream);
        }
    }

    private sealed class StreamReadingProcessingProvider : IDocumentProcessingProvider
    {
        public bool StreamWasRead { get; private set; }

        public async Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
        {
            Assert.NotNull(request.OpenContentStreamAsync);
            await using var content = await request.OpenContentStreamAsync(cancellationToken);
            using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            StreamWasRead = text.Contains("synthetic test document", StringComparison.Ordinal);

            var fields = new Dictionary<string, string> { ["sourceFileName"] = request.OriginalFileName };
            return new DocumentProcessingResult(
                new DocumentClassificationResult(DocumentType.ClaimForm, 0.99m, "StreamTest", DateTimeOffset.UtcNow),
                new DocumentExtractionResult(fields, fields.Keys.ToDictionary(key => key, _ => 0.99m), "StreamTest", DateTimeOffset.UtcNow, false, null, null),
                Array.Empty<DocumentValidationWarning>());
        }
    }

    private sealed class LowConfidenceProcessingProvider : IDocumentProcessingProvider
    {
        public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
        {
            var fields = new Dictionary<string, string> { ["amount"] = "UGX 18,500,000" };
            return Task.FromResult(new DocumentProcessingResult(
                new DocumentClassificationResult(DocumentType.GarageEstimate, 0.61m, "LowConfidenceTest", DateTimeOffset.UtcNow),
                new DocumentExtractionResult(fields, fields.Keys.ToDictionary(key => key, _ => 0.61m), "LowConfidenceTest", DateTimeOffset.UtcNow, false, null, null),
                [new DocumentValidationWarning("LOW_CONFIDENCE_AMOUNT", "Amount requires human verification.", "Warning")]));
        }
    }

    private sealed class TrackingReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return base.DisposeAsync();
        }
    }
}
