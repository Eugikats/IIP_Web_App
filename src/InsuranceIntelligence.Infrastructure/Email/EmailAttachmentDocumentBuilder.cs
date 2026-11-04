using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Email;

/// <summary>
/// Turns one downloaded email attachment into a stored, read and filed document on a claim. Shared by the Microsoft Graph and
/// IMAP attachment processors (build-loop item 28) so a file is treated the same way whichever way the mail arrived: stored
/// untouched, read, offered values for the claim's questions, checked for authenticity, and put in the slot it belongs to. A
/// file whose name says exactly which slot it is for ("police_report.pdf") goes there; otherwise the document reader files it
/// when it is sure, and a person looks when it is not.
/// </summary>
public sealed class EmailAttachmentDocumentBuilder(
    IDocumentStorageProvider storageProvider,
    IDocumentProcessingProvider processingProvider,
    InsuranceIntelligence.Application.Documents.IDocumentRecognitionService? recognition = null,
    InsuranceIntelligence.Application.Documents.IFieldProposalService? proposals = null,
    InsuranceIntelligence.Application.Documents.IAuthenticityService? authenticity = null,
    IDocumentChecklistProvider? checklistProvider = null,
    ICaseDocumentRepository? documentRepository = null)
{
    public async Task<CaseDocument> BuildAsync(
        ClaimCase claimCase,
        EmailAttachment attachment,
        GraphAttachmentContent content,
        string actorUserId,
        string sourceName,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid();
        var fileName = SafeFileName(string.IsNullOrWhiteSpace(content.FileName) ? attachment.FileName : content.FileName);
        var contentType = string.IsNullOrWhiteSpace(content.ContentType) ? attachment.ContentType : content.ContentType;
        var sizeBytes = content.SizeBytes > 0 ? content.SizeBytes : content.ContentBytes.LongLength;
        await using var stream = new MemoryStream(content.ContentBytes, writable: false);
        var storageResult = await storageProvider.StoreAsync(
            new DocumentStorageRequest(documentId, claimCase.CaseId, claimCase.Scope, fileName, contentType, sizeBytes, stream),
            cancellationToken);

        var uploaded = CreateUploadedDocument(documentId, claimCase, fileName, storageResult, actorUserId, correlationId, sourceName);
        var processingResult = await processingProvider.ProcessAsync(
            new DocumentProcessingRequest(
                documentId,
                claimCase.CaseId,
                uploaded.OriginalFileName,
                uploaded.ContentType,
                uploaded.FileSizeBytes,
                uploaded.ObjectKey,
                ct => storageProvider.ReadAsync(claimCase.Scope, uploaded.ObjectKey, ct)),
            cancellationToken);
        var processingFailed = processingResult.ValidationWarnings.Any(warning =>
            warning.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        var processed = uploaded with
        {
            DocumentType = processingResult.Classification.DocumentType,
            ProcessingStatus = processingFailed ? DocumentProcessingStatus.Failed : DocumentProcessingStatus.Processed,
            Classification = processingResult.Classification,
            Extraction = processingResult.Extraction,
            ValidationWarnings = processingResult.ValidationWarnings,
            ProcessedAt = DateTimeOffset.UtcNow,
        };

        if (checklistProvider is not null && documentRepository is not null && !processingFailed)
        {
            var existing = (await documentRepository.ListForCaseAsync(claimCase.CaseId, claimCase.Scope, cancellationToken))
                .Where(other => other.DocumentId != processed.DocumentId)
                .ToArray();
            var checklist = await checklistProvider.BuildChecklistAsync(claimCase, existing.Where(other => !other.IsSuperseded).ToArray(), cancellationToken);
            var named = Path.GetFileNameWithoutExtension(fileName);
            if (SlotCaptionMatcher.Match(named, checklist.Slots, existing) is { } slot && slot.Accepts(fileName))
            {
                processed = processed with { DocumentTypeKey = slot.Key, SlotKind = slot.Kind };
            }
        }

        if (recognition is not null && !processingFailed)
        {
            // Name it, and file it into the claim's matching slot when sure; otherwise a person looks.
            processed = await recognition.ApplyAsync(processed, claimCase, correlationId, cancellationToken);
        }

        if (proposals is not null && !processingFailed)
        {
            // Offer what it says for the claim's questions; an officer decides.
            await proposals.ProposeAsync(processed, claimCase, correlationId, cancellationToken);
        }

        if (authenticity is not null && !processingFailed)
        {
            await authenticity.AnalyseAsync(processed, claimCase, correlationId, cancellationToken);
        }

        return processed;
    }

    private static CaseDocument CreateUploadedDocument(
        Guid documentId, ClaimCase claimCase, string fileName, DocumentStorageResult storageResult, string uploadedBy, string correlationId, string sourceName)
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
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, sourceName, now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(), new Dictionary<string, decimal>(), sourceName, now,
                Approved: false, ApprovedByUserId: null, ApprovedAt: null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = uploadedBy,
            UploadedAt = now,
            CorrelationId = correlationId,
        };
    }

    private static string SafeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) ? "attachment" : safeName;
    }
}
