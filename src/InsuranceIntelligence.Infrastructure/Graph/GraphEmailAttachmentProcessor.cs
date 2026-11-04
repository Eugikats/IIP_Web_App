using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Graph;

public sealed class GraphEmailAttachmentProcessor(
    InfrastructureOptions options,
    IGraphMailClient graphClient,
    IDocumentStorageProvider storageProvider,
    IDocumentProcessingProvider processingProvider,
    InsuranceIntelligence.Application.Documents.IDocumentRecognitionService? recognition = null,
    InsuranceIntelligence.Application.Documents.IFieldProposalService? proposals = null,
    InsuranceIntelligence.Application.Documents.IAuthenticityService? authenticity = null) : IEmailAttachmentProcessor
{
    public async Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        InboundEmailMessage message,
        MailboxConnection? sourceMailbox,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (message.Attachments.Count == 0)
        {
            return Array.Empty<CaseDocument>();
        }

        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<CaseDocument>();
        }

        if (sourceMailbox is null)
        {
            throw new InvalidOperationException("Graph email attachment processing requires the source mailbox connection.");
        }

        if (string.IsNullOrWhiteSpace(message.ExternalMessageId))
        {
            throw new InvalidOperationException("Graph email attachment processing requires the Graph message id.");
        }

        var processedDocuments = new List<CaseDocument>();
        foreach (var attachment in message.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.ExternalAttachmentId))
            {
                throw new InvalidOperationException("Graph email attachment processing requires the Graph attachment id.");
            }

            var content = await graphClient.DownloadAttachmentAsync(
                sourceMailbox.MailboxAddress,
                message.ExternalMessageId,
                attachment.ExternalAttachmentId,
                cancellationToken);
            if (content.ContentBytes.Length == 0)
            {
                throw new InvalidOperationException("Graph email attachment content was empty.");
            }

            var documentId = Guid.NewGuid();
            var fileName = SafeFileName(string.IsNullOrWhiteSpace(content.FileName) ? attachment.FileName : content.FileName);
            var contentType = string.IsNullOrWhiteSpace(content.ContentType) ? attachment.ContentType : content.ContentType;
            var sizeBytes = content.SizeBytes > 0 ? content.SizeBytes : content.ContentBytes.LongLength;
            await using var stream = new MemoryStream(content.ContentBytes, writable: false);
            var storageResult = await storageProvider.StoreAsync(
                new DocumentStorageRequest(
                    documentId,
                    claimCase.CaseId,
                    claimCase.Scope,
                    fileName,
                    contentType,
                    sizeBytes,
                    stream),
                cancellationToken);

            var uploadedDocument = CreateUploadedDocument(
                documentId,
                claimCase,
                fileName,
                storageResult,
                actorUserId,
                $"graph-email-{message.MessageId:N}-{attachment.AttachmentId:N}");
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
            var processed = uploadedDocument with
            {
                DocumentType = processingResult.Classification.DocumentType,
                ProcessingStatus = processingFailed ? DocumentProcessingStatus.Failed : DocumentProcessingStatus.Processed,
                Classification = processingResult.Classification,
                Extraction = processingResult.Extraction,
                ValidationWarnings = processingResult.ValidationWarnings,
                ProcessedAt = DateTimeOffset.UtcNow
            };
            if (recognition is not null && !processingFailed)
            {
                // Name it, and file it into the claim's matching slot when sure; otherwise a person looks.
                processed = await recognition.ApplyAsync(processed, claimCase, $"graph-email-{message.MessageId:N}-{attachment.AttachmentId:N}", cancellationToken);
            }

            if (proposals is not null && !processingFailed)
            {
                // Offer what it says for the claim's questions; an officer decides.
                await proposals.ProposeAsync(processed, claimCase, $"graph-email-{message.MessageId:N}-{attachment.AttachmentId:N}", cancellationToken);
            }

            if (authenticity is not null && !processingFailed)
            {
                await authenticity.AnalyseAsync(processed, claimCase, $"graph-email-{message.MessageId:N}-{attachment.AttachmentId:N}", cancellationToken);
            }

            processedDocuments.Add(processed);
        }

        return processedDocuments;
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
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "GraphEmailAttachment", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "GraphEmailAttachment",
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

    private static string SafeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) ? "attachment" : safeName;
    }
}
