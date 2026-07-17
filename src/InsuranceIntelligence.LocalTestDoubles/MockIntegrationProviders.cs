using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class MockEmailIngestionProvider : IEmailIngestionProvider
{
    public Task<InboundEmailMessage> CreateTestMessageAsync(
        MailboxConnection mailbox,
        CancellationToken cancellationToken = default)
    {
        var messageId = Guid.NewGuid();
        return Task.FromResult(new InboundEmailMessage
        {
            MessageId = messageId,
            Scope = mailbox.Scope,
            MailboxConnectionId = mailbox.MailboxConnectionId,
            Sender = "claims@localtest.invalid",
            Subject = "Synthetic motor claim notification",
            BodyPreview = "Synthetic LocalTest message for claim intake verification.",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments =
            [
                new EmailAttachment(Guid.NewGuid(), messageId, "claim-form-localtest.pdf", "application/pdf", 2048),
                new EmailAttachment(Guid.NewGuid(), messageId, "photo-damage-localtest.jpg", "image/jpeg", 4096)
            ]
        });
    }
}

public sealed class MockEmailAttachmentProcessor : IEmailAttachmentProcessor
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
            ObjectKey = $"localtest-email/{message.MessageId:N}/{attachment.FileName}",
            DocumentType = Classify(attachment.FileName),
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(Classify(attachment.FileName), 0.82m, "LocalTest", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string> { ["source"] = "email_attachment", ["fileName"] = attachment.FileName },
                new Dictionary<string, decimal> { ["source"] = 1m, ["fileName"] = 1m },
                "LocalTest",
                now,
                false,
                null,
                null),
            ValidationWarnings = [],
            UploadedByUserId = actorUserId,
            UploadedAt = now,
            ProcessedAt = now,
            CorrelationId = $"localtest-email-{message.MessageId:N}"
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

public sealed class MockCommunicationSendingProvider : ICommunicationSendingProvider
{
    public Task<CommunicationSendResult> SendEmailAsync(
        CommunicationSendRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Send(request, "notification.email.localtest"));

    public Task<CommunicationSendResult> SendSmsAsync(
        CommunicationSendRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Send(request, "notification.sms.localtest"));

    private static CommunicationSendResult Send(CommunicationSendRequest request, string providerMessageId)
    {
        if (request.Channel == CommunicationChannel.SMS && !request.Recipient.StartsWith("+", StringComparison.Ordinal))
        {
            return new CommunicationSendResult("Failed", string.Empty, "SMS recipient must use international format.");
        }

        if (!request.Approved && !request.LowRisk)
        {
            return new CommunicationSendResult("ApprovalRequired", string.Empty, "Customer communication requires approval before sending.");
        }

        return new CommunicationSendResult("SimulatedSent", providerMessageId, null);
    }
}

public sealed class MockWebhookSubscriptionProvider : IWebhookSubscriptionProvider
{
    public Task<string> ValidateAsync(string payload, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(payload) ? "EmptyWebhookPayload" : "LocalTestWebhookAccepted");
}
