using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;

namespace InsuranceIntelligence.Api.Knowledge;

public static class KnowledgeEndpoints
{
    public const long MaxKnowledgeFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md",
        ".pdf",
        ".docx"
    };

    public static async Task<IResult> UploadKnowledgeDocument(
        IKnowledgeDocumentRepository repository,
        IKnowledgeStorageProvider storageProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (!IsMultipartFormData(httpContext.Request.ContentType))
        {
            return Validation("file", "Knowledge upload must use multipart/form-data.");
        }

        var form = httpContext.Features.Get<IFormFeature>()?.Form
            ?? await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null)
        {
            return Validation("file", "A knowledge document file is required.");
        }

        var fileValidation = ValidateFile(file);
        if (fileValidation is not null)
        {
            return Validation("file", fileValidation);
        }

        var metadataValidation = ValidateMetadata(form, out var metadata);
        if (metadataValidation is not null)
        {
            return metadataValidation;
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to upload knowledge.");
        }

        var correlationId = GetCorrelationId(httpContext);
        var documentId = Guid.NewGuid();
        await using var stream = file.OpenReadStream();
        var storageResult = await storageProvider.StoreAsync(
            new KnowledgeStorageRequest(
                documentId,
                scope,
                file.FileName,
                NormalizeContentType(file),
                file.Length,
                stream),
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var document = new KnowledgeDocument
        {
            KnowledgeDocumentId = documentId,
            Scope = scope,
            DocumentName = metadata.DocumentName,
            ProductLine = metadata.ProductLine,
            DocumentType = metadata.DocumentType,
            Version = metadata.Version,
            EffectiveDate = metadata.EffectiveDate,
            ExpiryDate = metadata.ExpiryDate,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = storageResult.ContentType,
            FileSizeBytes = storageResult.FileSizeBytes,
            ObjectKey = storageResult.ObjectKey,
            ApprovalStatus = KnowledgeDocumentStatus.Draft,
            IndexedStatus = KnowledgeIndexedStatus.NotIndexed,
            Chunks = BuildChunks(documentId, scope, metadata, storageResult),
            UploadedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };

        await repository.SaveAsync(document, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.KnowledgeUploaded(document, userId, correlationId), cancellationToken);

        return Results.Created($"/api/v1/knowledge-documents/{document.KnowledgeDocumentId}", KnowledgeMapper.ToResponse(document));
    }

    public static async Task<IResult> ListKnowledgeDocuments(
        IKnowledgeDocumentRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var documents = await repository.ListAsync(scope, cancellationToken);
        return Results.Ok(documents.Select(KnowledgeMapper.ToResponse).ToArray());
    }

    public static async Task<IResult> GetKnowledgeDocument(
        Guid knowledgeDocId,
        IKnowledgeDocumentRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var document = await repository.GetAsync(knowledgeDocId, scope, cancellationToken);
        return document is null
            ? Results.NotFound(new { message = "Knowledge document was not found for the requested tenant workspace." })
            : Results.Ok(KnowledgeMapper.ToDetailResponse(document));
    }

    public static async Task<IResult> ApproveKnowledgeDocument(
        Guid knowledgeDocId,
        IKnowledgeDocumentRepository repository,
        IKnowledgeIndexProvider indexProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var document = await repository.GetAsync(knowledgeDocId, scope, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Knowledge document was not found for the requested tenant workspace." });
        }

        var blockedReason = KnowledgeMapper.ApproveBlockedReason(document);
        if (blockedReason is not null)
        {
            return Results.Conflict(new { message = blockedReason, blockedAction = "approveKnowledgeDocument" });
        }

        var correlationId = GetCorrelationId(httpContext);
        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to approve knowledge.");
        }

        // WO-17: sensitivity is captured atomically with approval — a document can never become
        // Approved without a human explicitly classifying it (I3: no half-finished governance
        // state). Secrets/unapproved-PII classifications can never be approved for AI grounding at
        // all, regardless of reason — those categories describe content that should never be in
        // the corpus, not content pending a milder review.
        var (governanceRequest, governanceValidation) = await ReadGovernanceRequestAsync(httpContext, cancellationToken);
        if (governanceValidation is not null)
        {
            return governanceValidation;
        }

        if (!Enum.TryParse<KnowledgeSensitivity>(governanceRequest!.Sensitivity, ignoreCase: true, out var sensitivity)
            || sensitivity == KnowledgeSensitivity.Unknown)
        {
            return Validation("sensitivity", "A sensitivity classification is required to approve knowledge for AI grounding. Unknown sensitivity cannot be approved.");
        }

        if (sensitivity is KnowledgeSensitivity.ContainsSecrets or KnowledgeSensitivity.ContainsUnapprovedPii)
        {
            return Results.Conflict(new
            {
                message = "Content classified as containing secrets or unapproved PII can never be approved for AI grounding. Archive or replace this document instead.",
                blockedAction = "approveKnowledgeDocument"
            });
        }

        var auditReason = governanceRequest.GovernanceReason!;
        var approved = document with
        {
            ApprovalStatus = KnowledgeDocumentStatus.Approved,
            IndexedStatus = KnowledgeIndexedStatus.NotIndexed,
            Sensitivity = sensitivity,
            GovernanceReason = auditReason,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await repository.SaveAsync(approved, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.KnowledgeApproved(approved, userId, correlationId, auditReason),
            cancellationToken);

        var indexed = approved with
        {
            IndexedStatus = KnowledgeIndexedStatus.Indexed,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await indexProvider.IndexAsync(indexed, cancellationToken);
        await repository.SaveAsync(indexed, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.KnowledgeIndexed(indexed, correlationId), cancellationToken);

        return Results.Ok(KnowledgeMapper.ToResponse(indexed));
    }

    public static async Task<IResult> ArchiveKnowledgeDocument(
        Guid knowledgeDocId,
        IKnowledgeDocumentRepository repository,
        IKnowledgeIndexProvider indexProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var document = await repository.GetAsync(knowledgeDocId, scope, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Knowledge document was not found for the requested tenant workspace." });
        }

        var blockedReason = KnowledgeMapper.ArchiveBlockedReason(document);
        if (blockedReason is not null)
        {
            return Results.Conflict(new { message = blockedReason, blockedAction = "archiveKnowledgeDocument" });
        }

        var archived = document with
        {
            ApprovalStatus = KnowledgeDocumentStatus.Archived,
            IndexedStatus = KnowledgeIndexedStatus.NotIndexed,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var correlationId = GetCorrelationId(httpContext);
        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to archive knowledge.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to archive knowledge.");
        }

        await indexProvider.RemoveAsync(knowledgeDocId, scope, cancellationToken);
        await repository.SaveAsync(archived, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.KnowledgeArchived(archived, userId, correlationId, auditReason),
            cancellationToken);

        return Results.Ok(KnowledgeMapper.ToResponse(archived));
    }


    public static async Task<IResult> ReindexKnowledgeDocuments(
        IKnowledgeDocumentRepository repository,
        IKnowledgeIndexProvider indexProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var documents = await repository.ListAsync(scope, cancellationToken);
        var approved = documents
            .Where(document => document.ApprovalStatus == KnowledgeDocumentStatus.Approved && document.Sensitivity != KnowledgeSensitivity.Unknown)
            .Select(document => document with
            {
                IndexedStatus = KnowledgeIndexedStatus.Indexed,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToArray();

        await indexProvider.RebuildAsync(scope, approved, cancellationToken);
        foreach (var document in approved)
        {
            await repository.SaveAsync(document, cancellationToken);
        }
        await auditRepository.WriteAsync(AuditEvent.KnowledgeReindexed(scope, approved.Length, GetCorrelationId(httpContext)), cancellationToken);

        return Results.Ok(new { indexedDocumentCount = approved.Length });
    }

    private static IReadOnlyCollection<KnowledgeChunk> BuildChunks(
        Guid documentId,
        TenantScope scope,
        KnowledgeMetadata metadata,
        KnowledgeStorageResult storageResult)
    {
        if (string.IsNullOrWhiteSpace(storageResult.ExtractedText))
        {
            return Array.Empty<KnowledgeChunk>();
        }

        var normalized = string.Join(' ', storageResult.ExtractedText.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (normalized.Length == 0)
        {
            return Array.Empty<KnowledgeChunk>();
        }

        var chunks = new List<KnowledgeChunk>();
        var chunkSize = 600;
        var index = 0;

        for (var start = 0; start < normalized.Length; start += chunkSize)
        {
            var content = normalized.Substring(start, Math.Min(chunkSize, normalized.Length - start));
            var chunkId = $"{documentId:N}-{index}";
            chunks.Add(new KnowledgeChunk
            {
                ChunkId = chunkId,
                KnowledgeDocumentId = documentId,
                Scope = scope,
                DocumentName = metadata.DocumentName,
                ProductLine = metadata.ProductLine,
                Version = metadata.Version,
                ChunkIndex = index,
                Content = content,
                SourceReference = new KnowledgeSourceReference(
                    documentId,
                    chunkId,
                    metadata.DocumentName,
                    metadata.ProductLine,
                    metadata.Version,
                    storageResult.ObjectKey)
            });
            index++;

        }

        return chunks;
    }

    private static IResult? ValidateMetadata(IFormCollection form, out KnowledgeMetadata metadata)
    {
        metadata = default!;
        var documentName = form["documentName"].ToString().Trim();
        var productLine = form["productLine"].ToString().Trim();
        var version = form["version"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(documentName))
        {
            return Validation("documentName", "Document name is required.");
        }

        if (string.IsNullOrWhiteSpace(productLine))
        {
            return Validation("productLine", "Product line is required.");
        }

        if (!Enum.TryParse<KnowledgeDocumentType>(form["documentType"].ToString(), ignoreCase: true, out var documentType))
        {
            return Validation("documentType", "A supported knowledge document type is required.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Validation("version", "Version is required.");
        }

        if (!TryParseOptionalDate(form["effectiveDate"].ToString(), out var effectiveDate))
        {
            return Validation("effectiveDate", "Effective date must be a valid date.");
        }

        if (!TryParseOptionalDate(form["expiryDate"].ToString(), out var expiryDate))
        {
            return Validation("expiryDate", "Expiry date must be a valid date.");
        }

        metadata = new KnowledgeMetadata(documentName, productLine, documentType, version, effectiveDate, expiryDate);
        return null;
    }

    private static bool TryParseOptionalDate(string? value, out DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = null;
            return true;
        }

        if (DateOnly.TryParse(value, out var parsed))
        {
            date = parsed;
            return true;
        }

        date = null;
        return false;
    }

    private static string? ValidateFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            return "Uploaded knowledge documents cannot be empty.";
        }

        if (file.Length > MaxKnowledgeFileSizeBytes)
        {
            return "Uploaded knowledge documents cannot exceed 10 MB.";
        }

        var extension = Path.GetExtension(file.FileName);
        if (!SupportedExtensions.Contains(extension))
        {
            return "Unsupported file type. Supported knowledge types are TXT, MD, PDF, and DOCX.";
        }

        return null;
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static async Task<(KnowledgeGovernanceRequest? Request, IResult? Validation)> ReadGovernanceRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(httpContext.Request.ContentType)
            || !httpContext.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return (null, Validation("governanceReason", "A governance reason is required for knowledge lifecycle changes."));
        }

        KnowledgeGovernanceRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<KnowledgeGovernanceRequest>(
                httpContext.Request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
        }
        catch (JsonException)
        {
            return (null, Validation("governanceReason", "Knowledge governance request body must be valid JSON."));
        }

        if (string.IsNullOrWhiteSpace(request?.GovernanceReason))
        {
            return (request, Validation("governanceReason", "A governance reason is required for knowledge lifecycle changes."));
        }

        return (request, null);
    }

    private static bool IsMultipartFormData(string? contentType) =>
        contentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizeContentType(IFormFile file) =>
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }

    private sealed record KnowledgeMetadata(
        string DocumentName,
        string ProductLine,
        KnowledgeDocumentType DocumentType,
        string Version,
        DateOnly? EffectiveDate,
        DateOnly? ExpiryDate);
}
