using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http.Features;
namespace InsuranceIntelligence.Api.Documents;

public static class DocumentEndpoints
{
    /// <summary>Backward-compatible alias for configuration defaults; prefer <see cref="DocumentUploadValidation.MaxUploadFileSizeBytes"/>.</summary>
    public const long MaxLocalMockFileSizeBytes = DocumentUploadValidation.MaxUploadFileSizeBytes;

    public static async Task<IResult> UploadDocuments(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentChecklistProvider checklistProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        IAuditRepository auditRepository,
        INotificationEmitter notificationEmitter,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        ITenantConfigurationRepository? tenantConfigurationRepository = null,
        IDocumentTypeCatalogRepository? documentTypeCatalogRepository = null)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var confidenceThreshold = await ResolveConfidenceThresholdAsync(tenantConfigurationRepository, scope, cancellationToken);

        if (!IsMultipartFormData(httpContext.Request.ContentType))
        {
            return Validation("files", "Document upload must use multipart/form-data.");
        }

        var form = httpContext.Features.Get<IFormFeature>()?.Form
            ?? await httpContext.Request.ReadFormAsync(cancellationToken);
        IReadOnlyList<IFormFile> files = form.Files.GetFiles("files");
        if (files.Count == 0)
        {
            files = form.Files.ToArray();
        }

        if (files.Count == 0)
        {
            return Validation("files", "At least one document file is required.");
        }

        foreach (var file in files)
        {
            var validationResult = ValidateFile(file);
            if (validationResult is not null)
            {
                return Validation("files", validationResult);
            }
        }

        var correlationId = GetCorrelationId(httpContext);
        var uploadedBy = GetActor(httpContext, form["uploadedByUserId"].ToString());
        var selectedDocumentTypeKey = await ResolveSelectedDocumentTypeKeyAsync(
            form["documentTypeKey"].ToString(), scope, documentTypeCatalogRepository, cancellationToken);
        if (!selectedDocumentTypeKey.IsValid)
        {
            return Validation("documentTypeKey", selectedDocumentTypeKey.Error!);
        }

        var documents = new List<CaseDocument>();
        InsuranceIntelligence.Api.Local.ProviderNotConfiguredException? notConfiguredFailure = null;

        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var documentId = Guid.NewGuid();
            var storageResult = await storageProvider.StoreAsync(
                new DocumentStorageRequest(
                    documentId,
                    caseId,
                    scope,
                    file.FileName,
                    NormalizeContentType(file),
                    file.Length,
                    stream),
                cancellationToken);

            var uploadedDocument = CreateUploadedDocument(
                documentId, claimCase, file, storageResult, uploadedBy, correlationId, selectedDocumentTypeKey.Key);
            await documentRepository.SaveAsync(uploadedDocument, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.DocumentUploaded(uploadedDocument, uploadedBy, correlationId), cancellationToken);

            var run = await DocumentProcessingRunner.RunAsync(
                uploadedDocument,
                claimCase,
                storageProvider,
                processingProvider,
                fieldValidationRuleRepository,
                documentRepository,
                auditRepository,
                correlationId,
                cancellationToken);
            var processedDocument = run.Document;
            if (run.NotConfigured is not null)
            {
                notConfiguredFailure ??= run.NotConfigured;
            }

            if (run.Failed)
            {
                documents.Add(processedDocument);
                continue;
            }

            // Reuses the exact same low-confidence/warned/not-yet-approved condition that blocks
            // ApproveExtraction (DocumentActionState.BlockedActions), a document only notifies here
            // when it genuinely needs a human to verify it, not on every upload. Outcome K: the
            // notification names the actual reason (confidence score, threshold, warning count)
            // rather than a generic sentence.
            if (DocumentActionState.BlockedActions(processedDocument, confidenceThreshold).Count > 0)
            {
                await notificationEmitter.NotifyAsync(
                    scope,
                    claimCase.AssignedUserId ?? string.Empty,
                    NotificationType.DocumentReviewNeeded,
                    DocumentActionState.LowConfidenceReason(processedDocument, confidenceThreshold),
                    caseId,
                    cancellationToken);
            }

            documents.Add(processedDocument);
        }

        var updatedCase = await UpdateCompletenessAsync(
            claimCase,
            caseRepository,
            documentRepository,
            checklistProvider,
            auditRepository,
            scope,
            correlationId,
            writeWhenUnchanged: true,
            cancellationToken);
        if (notConfiguredFailure is not null)
        {
            // The files are stored and marked Failed (retryable), but an honest "not configured"
            // answer is what the caller needs: the structured 503 names the missing capability.
            throw notConfiguredFailure;
        }

        var allDocuments = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(updatedCase, allDocuments, cancellationToken);
        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);

        return Results.Ok(new DocumentUploadResponse(
            documents.Select(document => DocumentMapper.ToResponse(document, confidenceThreshold)).ToArray(),
            DocumentMapper.ToResponse(checklist),
            DocumentMapper.ToAuditSummary(auditEvents),
            notConfiguredFailure is not null ? "DocumentProcessing" : null));
    }

    public static async Task<IResult> ListDocuments(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        ITenantConfigurationRepository? tenantConfigurationRepository = null)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var confidenceThreshold = await ResolveConfidenceThresholdAsync(tenantConfigurationRepository, scope, cancellationToken);
        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(documents.Select(document => DocumentMapper.ToResponse(document, confidenceThreshold)).ToArray());
    }

    public static async Task<IResult> GetChecklist(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);

        return Results.Ok(DocumentMapper.ToResponse(checklist));
    }

    public static async Task<IResult> GetDocumentContent(
        Guid caseId,
        Guid documentId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var document = await documentRepository.GetAsync(caseId, documentId, scope, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Document was not found for the requested tenant workspace." });
        }

        if (!IsPreviewableContentType(document.ContentType))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Document preview unavailable",
                detail: "This document type cannot be previewed in the portal.");
        }

        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        httpContext.Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        var stream = await storageProvider.ReadAsync(scope, document.ObjectKey, cancellationToken);
        return Results.Stream(
            stream,
            document.ContentType,
            enableRangeProcessing: true);
    }

    /// <summary>Re-runs processing on a stored document that failed (or never finished), from the
    /// original stored file. Nothing is re-uploaded and nothing is lost.</summary>
    public static async Task<IResult> ReprocessDocument(
        Guid caseId,
        Guid documentId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        IDocumentChecklistProvider checklistProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        ITenantConfigurationRepository? tenantConfigurationRepository = null)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var document = await documentRepository.GetAsync(caseId, documentId, scope, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Document was not found for the requested tenant workspace." });
        }

        var blockedReason = DocumentMapper.ReprocessBlockedReason(document);
        if (blockedReason is not null)
        {
            return Results.Conflict(new { message = blockedReason, blockedAction = "reprocessDocument" });
        }

        var correlationId = GetCorrelationId(httpContext);
        var run = await DocumentProcessingRunner.RunAsync(
            document with { ProcessingStatus = DocumentProcessingStatus.Uploaded },
            claimCase,
            storageProvider,
            processingProvider,
            fieldValidationRuleRepository,
            documentRepository,
            auditRepository,
            correlationId,
            cancellationToken);
        await UpdateCompletenessAsync(
            claimCase, caseRepository, documentRepository, checklistProvider, auditRepository, scope, correlationId,
            writeWhenUnchanged: true, cancellationToken);
        if (run.NotConfigured is not null)
        {
            throw run.NotConfigured;
        }

        return Results.Ok(DocumentMapper.ToResponse(run.Document, await ResolveConfidenceThresholdAsync(tenantConfigurationRepository, scope, cancellationToken)));
    }

    public static async Task<IResult> ReclassifyDocument(
        Guid caseId,
        Guid documentId,
        ReclassifyDocumentRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IDocumentLabelledExampleRepository? labelledExampleRepository = null)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        if (!Enum.TryParse<DocumentType>(request.DocumentType, ignoreCase: true, out var documentType))
        {
            return Validation("documentType", "A supported document type is required.");
        }

        var document = await documentRepository.GetAsync(caseId, documentId, scope, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Document was not found for the requested tenant workspace." });
        }

        var blockedReason = DocumentMapper.ReclassifyBlockedReason(document);
        if (blockedReason is not null)
        {
            return Results.Conflict(new { message = blockedReason, blockedAction = "reclassifyDocument" });
        }

        var correlationId = GetCorrelationId(httpContext);
        var previousDocumentType = document.DocumentType.ToString();
        var updatedBy = GetActor(httpContext, request.UpdatedByUserId);
        var updatedDocument = document with
        {
            DocumentType = documentType,
            Classification = document.Classification with
            {
                DocumentType = documentType,
                ConfidenceScore = 1m,
                ClassifiedAt = DateTimeOffset.UtcNow
            },
            ProcessingStatus = document.ProcessingStatus == DocumentProcessingStatus.Uploaded
                ? DocumentProcessingStatus.Processed
                : document.ProcessingStatus
        };

        await documentRepository.SaveAsync(updatedDocument, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.DocumentReclassified(
                updatedDocument,
                updatedBy,
                previousDocumentType,
                correlationId,
                AuditReasonReader.Read(httpContext)),
            cancellationToken);

        // Outcome K, layer 3: every reclassification is reusable labelled data, not just a mutation
        // of this one document, so it accumulates toward this tenant's next classifier version.
        await RecordLabelledExampleAsync(
            labelledExampleRepository,
            new DocumentLabelledExample(
                Guid.NewGuid(),
                scope,
                documentId,
                caseId,
                document.Classification.DocumentType,
                document.Classification.ConfidenceScore,
                documentType,
                document.Extraction.Fields,
                DocumentLabelSource.Reclassification,
                updatedBy,
                DateTimeOffset.UtcNow,
                correlationId),
            cancellationToken);

        var updatedCase = await UpdateCompletenessAsync(
            claimCase,
            caseRepository,
            documentRepository,
            checklistProvider,
            auditRepository,
            scope,
            correlationId,
            writeWhenUnchanged: false,
            cancellationToken);
        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(updatedCase, documents, cancellationToken);

        return Results.Ok(new
        {
            document = DocumentMapper.ToResponse(updatedDocument),
            checklist = DocumentMapper.ToResponse(checklist)
        });
    }

    public static async Task<IResult> ApproveExtraction(
        Guid caseId,
        Guid documentId,
        ApproveExtractionRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IDocumentLabelledExampleRepository? labelledExampleRepository = null)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var document = await documentRepository.GetAsync(caseId, documentId, scope, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Document was not found for the requested tenant workspace." });
        }

        var blockedReason = DocumentMapper.ApproveExtractionBlockedReason(document);
        if (blockedReason is not null)
        {
            return Results.Conflict(new { message = blockedReason, blockedAction = "approveDocumentExtraction" });
        }

        var approvedBy = GetActor(httpContext, request.ApprovedByUserId);
        var approvedDocument = document with
        {
            ProcessingStatus = DocumentProcessingStatus.ExtractionApproved,
            Extraction = document.Extraction with
            {
                Approved = true,
                ApprovedByUserId = approvedBy,
                ApprovedAt = DateTimeOffset.UtcNow
            }
        };

        await documentRepository.SaveAsync(approvedDocument, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.DocumentExtractionApproved(
                approvedDocument,
                approvedBy,
                GetCorrelationId(httpContext),
                AuditReasonReader.Read(httpContext)),
            cancellationToken);

        // Outcome K, layer 3: approving without ever reclassifying is itself a labelled example,
        // the classifier's original guess confirmed correct. A confidence of exactly 1.0 means this
        // document was reclassified earlier in its life (ReclassifyDocument always sets it to 1m),
        // whose own labelled example already recorded the correction, so this branch skips it
        // rather than double-counting the same document as both a miss and a confirmed hit.
        if ((document.Extraction.FieldCorrections is null || document.Extraction.FieldCorrections.Count == 0)
            && document.Classification.ConfidenceScore < 1m)
        {
            await RecordLabelledExampleAsync(
                labelledExampleRepository,
                new DocumentLabelledExample(
                    Guid.NewGuid(),
                    scope,
                    documentId,
                    caseId,
                    document.Classification.DocumentType,
                    document.Classification.ConfidenceScore,
                    document.Classification.DocumentType,
                    document.Extraction.Fields,
                    DocumentLabelSource.ConfirmedWithoutCorrection,
                    approvedBy,
                    DateTimeOffset.UtcNow,
                    GetCorrelationId(httpContext)),
                cancellationToken);
        }

        return Results.Ok(DocumentMapper.ToResponse(approvedDocument));
    }

    public static async Task<IResult> CorrectExtractionField(
        Guid caseId,
        Guid documentId,
        CorrectExtractionFieldRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IAuditRepository auditRepository,
        IAgentWorkflowService agentWorkflowService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IDocumentLabelledExampleRepository? labelledExampleRepository = null)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var document = await documentRepository.GetAsync(caseId, documentId, scope, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Document was not found for the requested tenant workspace." });
        }

        var fieldName = request.FieldName?.Trim();
        if (string.IsNullOrWhiteSpace(fieldName) || fieldName.Length > 128)
        {
            return Validation("fieldName", "An extracted field name of 128 characters or fewer is required.");
        }

        var correctedValue = request.CorrectedValue?.Trim();
        if (string.IsNullOrWhiteSpace(correctedValue) || correctedValue.Length > 4000)
        {
            return Validation("correctedValue", "A corrected value of 4000 characters or fewer is required.");
        }

        var blockedReason = DocumentMapper.CorrectExtractionBlockedReason(document, fieldName);
        if (blockedReason is not null)
        {
            return Results.Conflict(new { message = blockedReason, blockedAction = "correctDocumentExtraction" });
        }

        var canonicalFieldName = document.Extraction.Fields.Keys.First(candidate =>
            candidate.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        var existingCorrections = document.Extraction.FieldCorrections
            ?? new Dictionary<string, DocumentFieldCorrection>();
        var corrections = new Dictionary<string, DocumentFieldCorrection>(existingCorrections, StringComparer.OrdinalIgnoreCase);
        var effectiveValue = corrections.TryGetValue(canonicalFieldName, out var existingCorrection)
            ? existingCorrection.CorrectedValue
            : document.Extraction.Fields[canonicalFieldName];
        if (effectiveValue.Equals(correctedValue, StringComparison.Ordinal))
        {
            return Results.Conflict(new
            {
                message = "The corrected value matches the current effective value.",
                blockedAction = "correctDocumentExtraction"
            });
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to correct an extracted field.");
        }

        var correlationId = GetCorrelationId(httpContext);
        var correctedBy = GetActor(httpContext, null);
        corrections[canonicalFieldName] = new DocumentFieldCorrection(
            canonicalFieldName,
            correctedValue,
            correctedBy,
            DateTimeOffset.UtcNow,
            correlationId);
        var correctedDocument = document with
        {
            Extraction = document.Extraction with { FieldCorrections = corrections }
        };

        // RF-05: invalidate before persisting so a failed re-queue cannot leave a
        // corrected document paired with stale completed downstream workflow state.
        await agentWorkflowService.InvalidateDownstreamAfterEvidenceCorrectionAsync(
            "case",
            caseId.ToString(),
            auditReason,
            cancellationToken);

        await documentRepository.SaveAsync(correctedDocument, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.DocumentExtractionFieldCorrected(
                correctedDocument,
                correctedBy,
                canonicalFieldName,
                correlationId,
                auditReason),
            cancellationToken);

        // Outcome K, layer 3: the retyped value is reusable labelled data too, even though it does
        // not change the document's type (OriginalPredictedType == ConfirmedType here by design).
        await RecordLabelledExampleAsync(
            labelledExampleRepository,
            new DocumentLabelledExample(
                Guid.NewGuid(),
                scope,
                documentId,
                caseId,
                document.DocumentType,
                document.Classification.ConfidenceScore,
                document.DocumentType,
                new Dictionary<string, string> { [canonicalFieldName] = correctedValue },
                DocumentLabelSource.FieldCorrection,
                correctedBy,
                DateTimeOffset.UtcNow,
                correlationId),
            cancellationToken);

        return Results.Ok(DocumentMapper.ToResponse(correctedDocument));
    }

    private static async Task<decimal> ResolveConfidenceThresholdAsync(
        ITenantConfigurationRepository? tenantConfigurationRepository,
        TenantScope scope,
        CancellationToken cancellationToken)
    {
        if (tenantConfigurationRepository is null)
        {
            return 0.8m;
        }

        var configuration = await tenantConfigurationRepository.GetAsync(scope, cancellationToken);
        return configuration.DocumentClassificationConfidenceThreshold;
    }

    private static async Task<(bool IsValid, string? Key, string? Error)> ResolveSelectedDocumentTypeKeyAsync(
        string? value,
        TenantScope scope,
        IDocumentTypeCatalogRepository? catalogRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (true, null, null);
        }

        var key = value.Trim();
        var current = catalogRepository is null
            ? Array.Empty<DocumentTypeDefinition>()
            : await catalogRepository.ListCurrentAsync(scope, cancellationToken);
        var definition = current.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (definition is not null)
        {
            return definition.Active
                ? (true, definition.Key, null)
                : (false, null, "This document type is inactive for the workspace.");
        }

        var builtIn = DefaultDocumentTypeCatalog.Defaults()
            .FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(builtIn.Key))
        {
            return (true, builtIn.Key, null);
        }

        return (false, null, "Choose a document type from the active workspace catalog.");
    }

    private static Task RecordLabelledExampleAsync(
        IDocumentLabelledExampleRepository? labelledExampleRepository,
        DocumentLabelledExample example,
        CancellationToken cancellationToken) =>
        labelledExampleRepository is null ? Task.CompletedTask : labelledExampleRepository.SaveAsync(example, cancellationToken);

    private static async Task<ClaimCase> UpdateCompletenessAsync(
        ClaimCase claimCase,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IAuditRepository auditRepository,
        TenantScope scope,
        string correlationId,
        bool writeWhenUnchanged,
        CancellationToken cancellationToken)
    {
        var documents = await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var updatedCase = claimCase with
        {
            CompletenessScore = checklist.CompletenessScore.Percentage,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (writeWhenUnchanged || updatedCase.CompletenessScore != claimCase.CompletenessScore)
        {
            await caseRepository.SaveAsync(updatedCase, cancellationToken);
            await auditRepository.WriteAsync(
                AuditEvent.CaseCompletenessUpdated(
                    claimCase.ToAuditSnapshot(),
                    updatedCase.ToAuditSnapshot(),
                    updatedCase.CompletenessScore,
                    correlationId),
                cancellationToken);
        }

        return updatedCase;
    }

    private static async Task<ClaimCase?> GetScopedCaseAsync(
        ICaseRepository caseRepository,
        Guid caseId,
        TenantScope scope,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        return claimCase is not null
            && claimCase.Scope.Matches(scope)
            && ClaimAccessGuard.CanViewCase(httpContext, claimCase)
            ? claimCase
            : null;
    }

    private static CaseDocument CreateUploadedDocument(
        Guid documentId,
        ClaimCase claimCase,
        IFormFile file,
        DocumentStorageResult storageResult,
        string uploadedBy,
        string correlationId,
        string? documentTypeKey)
    {
        var now = DateTimeOffset.UtcNow;

        return new CaseDocument
        {
            DocumentId = documentId,
            CaseId = claimCase.CaseId,
            Scope = claimCase.Scope,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = storageResult.ContentType,
            FileSizeBytes = storageResult.FileSizeBytes,
            ObjectKey = storageResult.ObjectKey,
            DocumentType = DocumentType.Unknown,
            DocumentTypeKey = documentTypeKey,
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "NotConfigured", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "NotConfigured",
                now,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = uploadedBy,
            UploadedAt = now,
            CorrelationId = correlationId
        };
    }

    // WO-15: lets FNOL intake show the real required-documents checklist for the selected product
    // line before a case exists, instead of a hardcoded client list (A1), reuses the exact same
    // DocumentChecklistRules the platform uses everywhere else once a case is created.
    public static async Task<IResult> PreviewChecklist(
        string? productLine,
        string? claimType,
        string? injuryIndicator,
        HttpContext httpContext,
        InfrastructureOptions options,
        IProductRepository? productRepository = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(productLine))
        {
            return Validation("productLine", "A product line is required to preview the required documents.");
        }

        var motorFnolDetails = string.IsNullOrWhiteSpace(injuryIndicator)
            ? null
            : new MotorFnolDetails(null, null, null, null, null, null, injuryIndicator, null, null, null);
        // The same resolution a filed claim gets: the published product for this line and the claim
        // type the person chose, falling back to the built-in rules only when none applies.
        string? claimTypeKey = null;
        DocumentChecklistRuleSet? ruleSet = null;
        if (productRepository is not null)
        {
            var product = (await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken))
                .FirstOrDefault(candidate => candidate.Status == ProductStatus.Published
                    && string.Equals(candidate.ProductClass, productLine.Trim(), StringComparison.OrdinalIgnoreCase));
            if (product is not null)
            {
                claimTypeKey = (product.FindClaimType(claimType) ?? product.SuggestClaimType(claimType))?.Key;
                ruleSet = DocumentChecklistRules.ForProduct(product, claimTypeKey);
            }
        }

        ruleSet ??= DocumentChecklistRules.Resolve(productLine, claimType, motorFnolDetails);
        var slots = ruleSet.Slots
            .Select(slot => new ChecklistSlotResponse(
                slot.Key, slot.Label, slot.Description, slot.Required, slot.Kind.ToString(),
                slot.AllowsMultipleFiles, slot.MaxFiles, slot.AcceptedFormats, Received: false))
            .ToArray();

        return Results.Ok(new ChecklistPreviewResponse(
            ruleSet.RequiredDocuments.Select(ToPreview).ToArray(),
            ruleSet.OptionalDocuments.Select(ToPreview).ToArray(),
            slots,
            claimTypeKey));
    }

    private static ChecklistDocumentPreview ToPreview(DocumentType documentType) =>
        new(documentType.ToString(), DocumentMapper.LabelFor(documentType));

    private static string? ValidateFile(IFormFile file) =>
        DocumentUploadValidation.Validate(file.Length, file.FileName);

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static string NormalizeContentType(IFormFile file) =>
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

    private static bool IsMultipartFormData(string? contentType) =>
        contentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsPreviewableContentType(string contentType) =>
        contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveScope(
        HttpContext httpContext,
        out TenantScope scope,
        out string userId,
        out string validationError)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } tenantContext)
        {
            var current = tenantContext.Current;
            scope = TenantScope.Create(current.TenantId, current.WorkspaceId);
            userId = current.UserId;
            validationError = string.Empty;
            return true;
        }

        var options = httpContext.RequestServices?.GetService(typeof(InfrastructureOptions)) as InfrastructureOptions;
        if (options is not null
            && !options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            // Protected runtimes: membership-resolved TenantContext only (ADR-0005).
            // Do not fall back to deployment config or client headers/query.
            scope = new TenantScope(string.Empty, string.Empty);
            userId = string.Empty;
            validationError = "Server-resolved tenant context is required.";
            return false;
        }

        if (TenantScopeReader.TryGetFromHeaders(httpContext, out scope, out validationError))
        {
            userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
            return true;
        }

        userId = string.Empty;
        return false;
    }

    private static string GetActor(HttpContext httpContext, string? candidate)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } tenantContext
            && !string.IsNullOrWhiteSpace(tenantContext.Current.UserId))
        {
            return tenantContext.Current.UserId;
        }

        var options = httpContext.RequestServices?.GetService(typeof(InfrastructureOptions)) as InfrastructureOptions;
        if (options is not null
            && !options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            // Never accept form-field or header actor spoofing outside Development.
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate.Trim();
        }

        if (httpContext.Request.Headers.TryGetValue("X-User-Id", out var userId)
            && !string.IsNullOrWhiteSpace(userId))
        {
            return userId.ToString();
        }

        return "local-mock-user";
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}
