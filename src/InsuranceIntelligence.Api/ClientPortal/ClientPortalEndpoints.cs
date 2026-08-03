using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.AspNetCore.Http.Features;

namespace InsuranceIntelligence.Api.ClientPortal;

public static class ClientPortalEndpoints
{
    public static async Task<IResult> GetClaim(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        ICommunicationRepository communicationRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var communications = await communicationRepository.ListForCaseAsync(caseId, scope, cancellationToken);

        return Results.Ok(ToResponse(claimCase, checklist, documents, communications));
    }

    public static async Task<IResult> UploadDocuments(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentChecklistProvider checklistProvider,
        ICommunicationRepository communicationRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

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
            var fileError = ValidateUploadFile(file);
            if (fileError is not null)
            {
                return Validation("files", fileError);
            }
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, "client-portal-user");
        var correlationId = httpContext.TraceIdentifier;
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

            var uploadedDocument = CreateUploadedDocument(documentId, claimCase, file, storageResult, actor, correlationId);
            await documentRepository.SaveAsync(uploadedDocument, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.DocumentUploaded(uploadedDocument, actor, correlationId), cancellationToken);

            var processingResult = await processingProvider.ProcessAsync(
                new DocumentProcessingRequest(
                    documentId,
                    caseId,
                    uploadedDocument.OriginalFileName,
                    uploadedDocument.ContentType,
                    uploadedDocument.FileSizeBytes,
                    uploadedDocument.ObjectKey,
                    ct => storageProvider.ReadAsync(scope, uploadedDocument.ObjectKey, ct)),
                cancellationToken);
            var processedDocument = uploadedDocument with
            {
                DocumentType = processingResult.Classification.DocumentType,
                ProcessingStatus = DocumentProcessingStatus.Processed,
                Classification = processingResult.Classification,
                Extraction = processingResult.Extraction,
                ValidationWarnings = processingResult.ValidationWarnings,
                ProcessedAt = DateTimeOffset.UtcNow
            };

            await documentRepository.SaveAsync(processedDocument, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.DocumentProcessed(processedDocument, correlationId), cancellationToken);
        }

        var allDocuments = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, allDocuments, cancellationToken);
        var updatedCase = claimCase with
        {
            CompletenessScore = checklist.CompletenessScore.Percentage,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await caseRepository.SaveAsync(updatedCase, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseCompletenessUpdated(
                claimCase.ToAuditSnapshot(),
                updatedCase.ToAuditSnapshot(),
                updatedCase.CompletenessScore,
                correlationId),
            cancellationToken);

        var communications = await communicationRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(ToResponse(updatedCase, checklist, allDocuments, communications));
    }

    private static ClientPortalClaimResponse ToResponse(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        IReadOnlyCollection<CaseDocument> documents,
        IReadOnlyCollection<Communication> communications)
    {
        var receivedDocumentTypes = documents
            .Select(document => document.DocumentType)
            .ToHashSet();

        var requiredDocuments = checklist.RequiredDocuments
            .Select(documentType => new ClientPortalDocumentRequirement(
                documentType.ToString(),
                Label(documentType),
                receivedDocumentTypes.Contains(documentType),
                receivedDocumentTypes.Contains(documentType) ? "Uploaded" : "Not uploaded"))
            .ToArray();

        var messages = communications
            .Where(communication => IsCustomerSafeMessage(communication.MessageBody))
            .OrderByDescending(communication => communication.CreatedAt)
            .Take(10)
            .Select(communication => new ClientPortalMessage(
                communication.CommunicationId,
                communication.Channel.ToString(),
                communication.Direction,
                communication.MessageBody,
                communication.CreatedAt,
                communication.DeliveryStatus))
            .ToArray();

        return new ClientPortalClaimResponse(
            claimCase.CaseId,
            claimCase.ProductLine,
            claimCase.ClaimType,
            claimCase.Status.ToString(),
            claimCase.PolicyNumber,
            claimCase.IncidentDate,
            claimCase.CreatedAt,
            claimCase.UpdatedAt,
            checklist.CompletenessScore.Percentage,
            requiredDocuments,
            BuildTimeline(claimCase),
            messages,
            BuildNextSteps(checklist));
    }

    private static IReadOnlyCollection<ClientPortalTimelineStep> BuildTimeline(ClaimCase claimCase)
    {
        var underReview = claimCase.Status is not CaseStatus.New;
        var assessment = claimCase.Status is CaseStatus.ReadyForReview or CaseStatus.Escalated or CaseStatus.Settled or CaseStatus.Closed;
        var decision = claimCase.Status is CaseStatus.Settled or CaseStatus.Closed;

        return
        [
            new("Claim received", true, claimCase.CreatedAt),
            new("Under review", underReview, underReview ? claimCase.UpdatedAt : null),
            new("Assessment", assessment, null),
            new("Decision", decision, null),
            new("Settlement", claimCase.Status is CaseStatus.Settled or CaseStatus.Closed, null)
        ];
    }

    private static IReadOnlyCollection<string> BuildNextSteps(DocumentCompletenessChecklist checklist)
    {
        if (checklist.MissingRequiredDocuments.Count > 0)
        {
            return
            [
                "Upload the remaining required documents.",
                "Our team will review the information received.",
                "We will share the next update through your configured contact channel."
            ];
        }

        return
        [
            "No required documents are missing right now.",
            "Our team will complete the claim review.",
            "We will share the decision with you through your configured contact channel."
        ];
    }

    private static bool BelongsToCustomer(ClaimCase claimCase, HttpContext httpContext)
    {
        var userId = WorkflowEndpoints.GetActor(httpContext, string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return EqualsNormalized(userId, claimCase.CustomerEmail)
            || EqualsNormalized(userId, claimCase.CustomerPhone)
            || EqualsNormalized(userId, claimCase.CreatedByUserId);
    }

    private static bool EqualsNormalized(string left, string? right) =>
        !string.IsNullOrWhiteSpace(right)
        && Normalize(left) == Normalize(right);

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool IsCustomerSafeMessage(string value) =>
        !new[] { "raw prompt", "system message", "internal note", "fraud flag", "reserve", "hidden risk", "secret", "confidence" }
            .Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static CaseDocument CreateUploadedDocument(
        Guid documentId,
        ClaimCase claimCase,
        IFormFile file,
        DocumentStorageResult storageResult,
        string uploadedBy,
        string correlationId)
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
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "ClientPortal", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "ClientPortal",
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

    private static string? ValidateUploadFile(IFormFile file) =>
        DocumentUploadValidation.Validate(file.Length, file.FileName);

    private static bool IsMultipartFormData(string? contentType) =>
        contentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizeContentType(IFormFile file) =>
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static string Label(DocumentType documentType) =>
        documentType switch
        {
            DocumentType.ClaimForm => "Completed claim form",
            DocumentType.PoliceAbstract => "Police report",
            DocumentType.PhotoEvidence => "Photos of the damage",
            DocumentType.Invoice => "Repair estimate or invoice",
            DocumentType.MedicalReport => "Medical report",
            DocumentType.CustomerId => "Customer identification",
            DocumentType.GarageEstimate => "Garage estimate",
            _ => "Supporting document"
        };
}

public sealed record ClientPortalClaimResponse(
    Guid CaseId,
    string ProductLine,
    string ClaimType,
    string Status,
    string PolicyNumber,
    DateOnly IncidentDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal CompletenessScore,
    IReadOnlyCollection<ClientPortalDocumentRequirement> RequiredDocuments,
    IReadOnlyCollection<ClientPortalTimelineStep> Timeline,
    IReadOnlyCollection<ClientPortalMessage> Messages,
    IReadOnlyCollection<string> NextSteps);

public sealed record ClientPortalDocumentRequirement(
    string DocumentType,
    string Label,
    bool Uploaded,
    string Status);

public sealed record ClientPortalTimelineStep(
    string Label,
    bool Complete,
    DateTimeOffset? Date);

public sealed record ClientPortalMessage(
    Guid CommunicationId,
    string Channel,
    string Direction,
    string MessageBody,
    DateTimeOffset CreatedAt,
    string DeliveryStatus);
