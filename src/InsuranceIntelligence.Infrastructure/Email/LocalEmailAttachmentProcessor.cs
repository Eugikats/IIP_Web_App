using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Email;

/// <summary>
/// Synthetic email-attachment processing for local/offline runs (EmailProvider != "Graph") — the
/// sanctioned Synthetic provider path. Classifies attachments by filename convention only (no real
/// document-intelligence call), so a message from <see cref="LocalEmailIngestionProvider"/> can be
/// processed into a claim with real (if synthetic) documents rather than failing closed.
/// </summary>
public sealed class LocalEmailAttachmentProcessor : IEmailAttachmentProcessor
{
    public Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        InboundEmailMessage message,
        MailboxConnection? sourceMailbox,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var documents = message.Attachments.Select(attachment => new CaseDocument
        {
            DocumentId = Guid.NewGuid(),
            CaseId = claimCase.CaseId,
            Scope = claimCase.Scope,
            OriginalFileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.SizeBytes,
            ObjectKey = $"synthetic-email/{message.MessageId:N}/{attachment.FileName}",
            DocumentType = Classify(attachment.FileName),
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(Classify(attachment.FileName), 0.82m, "SyntheticEmailIngestion", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string> { ["source"] = "email_attachment", ["fileName"] = attachment.FileName },
                new Dictionary<string, decimal> { ["source"] = 1m, ["fileName"] = 1m },
                "SyntheticEmailIngestion",
                now,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ValidationWarnings = [],
            UploadedByUserId = actorUserId,
            UploadedAt = now,
            ProcessedAt = now,
            CorrelationId = $"synthetic-email-{message.MessageId:N}"
        }).ToArray();

        return Task.FromResult<IReadOnlyCollection<CaseDocument>>(documents);
    }

    private static DocumentType Classify(string fileName) =>
        fileName.Contains("claim", StringComparison.OrdinalIgnoreCase)
            ? DocumentType.ClaimForm
            : fileName.Contains("photo", StringComparison.OrdinalIgnoreCase)
                ? DocumentType.PhotoEvidence
                : fileName.Contains("police", StringComparison.OrdinalIgnoreCase)
                    ? DocumentType.PoliceAbstract
                    : DocumentType.Unknown;
}
