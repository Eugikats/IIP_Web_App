using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Email;

/// <summary>
/// Synthetic email ingestion for local/offline runs (EmailProvider != "Graph") — the sanctioned
/// Synthetic provider path, not a test double. Lets "Run test ingestion" in the Email Mailboxes
/// admin screen actually produce an inbound message end to end without a real mailbox connection.
/// Real deployments configure EmailProvider=Graph, where this type is registered but never called
/// (TestIngestion pulls live mail through IGraphMailProvider instead).
/// </summary>
public sealed class LocalEmailIngestionProvider : IEmailIngestionProvider
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
            Sender = "claims@synthetic.iip.local",
            Subject = "Synthetic motor claim notification",
            BodyPreview = "Synthetic message for local claim-intake verification. No real customer data.",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments =
            [
                new EmailAttachment(Guid.NewGuid(), messageId, "claim-form-synthetic.pdf", "application/pdf", 2048),
                new EmailAttachment(Guid.NewGuid(), messageId, "photo-damage-synthetic.jpg", "image/jpeg", 4096)
            ]
        });
    }
}
