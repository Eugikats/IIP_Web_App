using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// Routes inbound WhatsApp media onto the existing document storage + processing path
/// (same spine as <c>GraphEmailAttachmentProcessor</c>). Allowed MIME types stay aligned with
/// portal upload until a deliberate expansion (e.g. image/webp) is approved.
/// </summary>
public sealed class WhatsAppAttachmentProcessor(
    InfrastructureOptions options,
    IWhatsAppInboundProvider whatsAppProvider,
    IDocumentStorageProvider storageProvider,
    IDocumentProcessingProvider processingProvider) : IWhatsAppAttachmentProcessor
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/tiff",
        "text/plain",
    };

    public async Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        WhatsAppInboundMediaMessage message,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!options.ChannelProvider.Equals("WhatsAppCloudApi", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<CaseDocument>();
        }

        if (!whatsAppProvider.IsConfigured)
        {
            throw new InvalidOperationException("WhatsApp provider is not configured.");
        }

        var content = await whatsAppProvider.DownloadMediaAsync(message.MediaId, cancellationToken);
        var contentType = string.IsNullOrWhiteSpace(content.ContentType)
            ? message.MimeType ?? "application/octet-stream"
            : content.ContentType;
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException(
                $"WhatsApp media content type '{contentType}' is not an allowed claim document type.");
        }

        if (content.ContentBytes.Length == 0)
        {
            throw new InvalidOperationException("WhatsApp media content was empty.");
        }

        var documentId = Guid.NewGuid();
        var fileName = SafeFileName(
            string.IsNullOrWhiteSpace(message.FileName) ? content.FileName : message.FileName!);
        await using var stream = new MemoryStream(content.ContentBytes, writable: false);
        var storageResult = await storageProvider.StoreAsync(
            new DocumentStorageRequest(
                documentId,
                claimCase.CaseId,
                claimCase.Scope,
                fileName,
                contentType,
                content.ContentBytes.LongLength,
                stream),
            cancellationToken);

        var uploadedDocument = CreateUploadedDocument(
            documentId,
            claimCase,
            fileName,
            storageResult,
            actorUserId,
            $"whatsapp-{message.ProviderMessageId}");
        var processingResult = await processingProvider.ProcessAsync(
            new DocumentProcessingRequest(
                documentId,
                claimCase.CaseId,
                uploadedDocument.OriginalFileName,
                uploadedDocument.ContentType,
                uploadedDocument.FileSizeBytes,
                uploadedDocument.ObjectKey,
                ct => storageProvider.ReadAsync(claimCase.Scope, uploadedDocument.ObjectKey, ct)),
            cancellationToken);
        var processingFailed = processingResult.ValidationWarnings.Any(warning =>
            warning.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));

        return
        [
            uploadedDocument with
            {
                DocumentType = processingResult.Classification.DocumentType,
                ProcessingStatus = processingFailed
                    ? DocumentProcessingStatus.Failed
                    : DocumentProcessingStatus.Processed,
                Classification = processingResult.Classification,
                Extraction = processingResult.Extraction,
                ValidationWarnings = processingResult.ValidationWarnings,
                ProcessedAt = DateTimeOffset.UtcNow,
            },
        ];
    }

    private static CaseDocument CreateUploadedDocument(
        Guid documentId,
        ClaimCase claimCase,
        string fileName,
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
            OriginalFileName = fileName,
            ContentType = storageResult.ContentType,
            FileSizeBytes = storageResult.FileSizeBytes,
            ObjectKey = storageResult.ObjectKey,
            DocumentType = DocumentType.Unknown,
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "WhatsAppCloudApi", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "WhatsAppCloudApi",
                now,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = uploadedBy,
            UploadedAt = now,
            CorrelationId = correlationId,
        };
    }

    private static string SafeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) ? "whatsapp-attachment" : safeName;
    }
}

public sealed class DisabledWhatsAppAttachmentProcessor : IWhatsAppAttachmentProcessor
{
    public Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        WhatsAppInboundMediaMessage message,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<CaseDocument>>(Array.Empty<CaseDocument>());
}
