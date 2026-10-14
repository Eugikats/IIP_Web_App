using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Application.Knowledge;

/// <summary>What a file must be to enter the Data Hub, whoever brings it: a person uploading, or a connector syncing.</summary>
public static class KnowledgeIngestionLimits
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".pdf", ".docx" };

    /// <summary>Null when the file may enter, otherwise a plain reason it may not.</summary>
    public static string? WhyNot(string fileName, long length)
    {
        if (length <= 0) return "Uploaded knowledge documents cannot be empty.";
        if (length > MaxFileSizeBytes) return "Uploaded knowledge documents cannot exceed 10 MB.";
        if (!SupportedExtensions.Contains(Path.GetExtension(fileName))) return "Unsupported file type. Supported knowledge types are TXT, MD, PDF, and DOCX.";
        return null;
    }
}

public sealed record KnowledgeIngestionRequest(
    TenantScope Scope,
    string UserId,
    string DocumentName,
    string ProductLine,
    KnowledgeDocumentType DocumentType,
    string Version,
    DateOnly? EffectiveDate,
    DateOnly? ExpiryDate,
    string FileName,
    string ContentType,
    long Length,
    Stream Content,
    string CorrelationId);

/// <summary>
/// The one way a file becomes a Knowledge document. It stores the file, cuts it into cited chunks, saves it and writes
/// the audit entry. What it creates is always a Draft with unknown sensitivity: nothing that arrives here, by upload or
/// by a connector, is approved, indexed for customers or used to ground an answer until a person classifies and approves it.
/// </summary>
public interface IKnowledgeIngestionSink
{
    Task<KnowledgeDocument> IngestPendingAsync(KnowledgeIngestionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a document out of use at once: archived, removed from the search index, audited with the reason. Used when
    /// the file it came from is gone, changed or no longer in a synced folder, so nothing stale keeps answering.
    /// </summary>
    Task RetireAsync(TenantScope scope, Guid knowledgeDocumentId, string actor, string reason, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class KnowledgeIngestionService(
    IKnowledgeDocumentRepository repository,
    IKnowledgeStorageProvider storageProvider,
    IAuditRepository auditRepository,
    IKnowledgeIndexProvider? indexProvider = null) : IKnowledgeIngestionSink
{
    public async Task RetireAsync(TenantScope scope, Guid knowledgeDocumentId, string actor, string reason, string correlationId, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetAsync(knowledgeDocumentId, scope, cancellationToken);
        if (document is null || document.ApprovalStatus == KnowledgeDocumentStatus.Archived)
        {
            return;
        }

        if (indexProvider is not null)
        {
            await indexProvider.RemoveAsync(knowledgeDocumentId, scope, cancellationToken);
        }

        var archived = document with
        {
            ApprovalStatus = KnowledgeDocumentStatus.Archived,
            IndexedStatus = KnowledgeIndexedStatus.NotIndexed,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await repository.SaveAsync(archived, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.KnowledgeArchived(archived, actor, correlationId, reason), cancellationToken);
    }

    public async Task<KnowledgeDocument> IngestPendingAsync(KnowledgeIngestionRequest request, CancellationToken cancellationToken = default)
    {
        var documentId = Guid.NewGuid();
        var storageResult = await storageProvider.StoreAsync(
            new KnowledgeStorageRequest(documentId, request.Scope, request.FileName, request.ContentType, request.Length, request.Content),
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var document = new KnowledgeDocument
        {
            KnowledgeDocumentId = documentId,
            Scope = request.Scope,
            DocumentName = request.DocumentName,
            ProductLine = request.ProductLine,
            DocumentType = request.DocumentType,
            Version = request.Version,
            EffectiveDate = request.EffectiveDate,
            ExpiryDate = request.ExpiryDate,
            OriginalFileName = Path.GetFileName(request.FileName),
            ContentType = storageResult.ContentType,
            FileSizeBytes = storageResult.FileSizeBytes,
            ObjectKey = storageResult.ObjectKey,
            ApprovalStatus = KnowledgeDocumentStatus.Draft,
            IndexedStatus = KnowledgeIndexedStatus.NotIndexed,
            Chunks = BuildChunks(documentId, request, storageResult),
            UploadedByUserId = request.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = request.CorrelationId
        };

        await repository.SaveAsync(document, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.KnowledgeUploaded(document, request.UserId, request.CorrelationId), cancellationToken);
        return document;
    }

    private static IReadOnlyCollection<KnowledgeChunk> BuildChunks(Guid documentId, KnowledgeIngestionRequest request, KnowledgeStorageResult storageResult)
    {
        if (string.IsNullOrWhiteSpace(storageResult.ExtractedText))
        {
            return Array.Empty<KnowledgeChunk>();
        }

        // Cut at paragraphs and sentences, keeping the heading each piece sits under, so an answer can cite a
        // section and a rule is not split from the sentence that introduces it.
        var chunks = new List<KnowledgeChunk>();
        var index = 0;
        foreach (var piece in KnowledgeChunker.Chunk(storageResult.ExtractedText))
        {
            var chunkId = $"{documentId:N}-{index}";
            chunks.Add(new KnowledgeChunk
            {
                ChunkId = chunkId,
                KnowledgeDocumentId = documentId,
                Scope = request.Scope,
                DocumentName = request.DocumentName,
                ProductLine = request.ProductLine,
                Version = request.Version,
                ChunkIndex = index,
                Content = piece.Content,
                Section = piece.Section,
                SourceReference = new KnowledgeSourceReference(
                    documentId,
                    chunkId,
                    request.DocumentName,
                    request.ProductLine,
                    request.Version,
                    storageResult.ObjectKey)
            });
            index++;
        }

        return chunks;
    }
}
