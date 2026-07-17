using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Email;

public sealed class DisabledEmailAttachmentProcessor : IEmailAttachmentProcessor
{
    public Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        InboundEmailMessage message,
        MailboxConnection? sourceMailbox,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Email attachment processing is not configured. Configure Graph email and document providers before processing attachments.");
}
