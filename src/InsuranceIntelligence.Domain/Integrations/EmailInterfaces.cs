using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Integrations;

public interface IMailboxConnectionRepository
{
    Task SaveAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MailboxConnection>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<MailboxConnection?> GetAsync(Guid mailboxConnectionId, TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>The workspaces that have at least one mailbox with collection switched on. Deliberately not scoped: the
    /// scheduled collection visits every workspace that connected a mailbox (build-loop item 28). Returns scopes only, never
    /// mailbox details.</summary>
    Task<IReadOnlyList<TenantScope>> ListScopesWithIngestionAsync(CancellationToken cancellationToken = default);
}

public interface IInboundEmailRepository
{
    Task SaveAsync(InboundEmailMessage message, CancellationToken cancellationToken = default);

    Task<InboundEmailMessage?> GetAsync(Guid messageId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InboundEmailMessage>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

public interface IEmailIngestionProvider
{
    Task<InboundEmailMessage> CreateTestMessageAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default);
}

public interface IGraphMailProvider
{
    Task<IReadOnlyCollection<InboundEmailMessage>> PullMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default);

    Task CompleteMessageAsync(MailboxConnection mailbox, InboundEmailMessage message, CancellationToken cancellationToken = default);
}

public interface IGraphMailClient
{
    Task<IReadOnlyCollection<GraphMailboxMessage>> ReadMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default);

    Task<GraphAttachmentContent> DownloadAttachmentAsync(
        string mailboxAddress,
        string externalMessageId,
        string externalAttachmentId,
        CancellationToken cancellationToken = default);

    /// <summary>Same, against the insurer's own Microsoft Entra directory (build-loop item 28). A client that only knows the
    /// deployment's directory ignores it.</summary>
    Task<GraphAttachmentContent> DownloadAttachmentAsync(
        string mailboxAddress,
        string externalMessageId,
        string externalAttachmentId,
        string? entraTenantId,
        CancellationToken cancellationToken = default) =>
        DownloadAttachmentAsync(mailboxAddress, externalMessageId, externalAttachmentId, cancellationToken);

    Task MoveMessageAsync(string mailboxAddress, string externalMessageId, string folderName, CancellationToken cancellationToken = default);

    Task MoveMessageAsync(string mailboxAddress, string externalMessageId, string folderName, string? entraTenantId, CancellationToken cancellationToken = default) =>
        MoveMessageAsync(mailboxAddress, externalMessageId, folderName, cancellationToken);

    Task<GraphMailSendResult> SendMailAsync(GraphMailSendRequest request, CancellationToken cancellationToken = default);
}

public interface IEmailAttachmentProcessor
{
    Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        InboundEmailMessage message,
        MailboxConnection? sourceMailbox,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

public interface IEmailToCaseMatcher
{
    Task<EmailToCaseMatchResult> MatchAsync(InboundEmailMessage message, TenantScope scope, CancellationToken cancellationToken = default);
}

public interface IWebhookSubscriptionProvider
{
    Task<string> ValidateAsync(string payload, CancellationToken cancellationToken = default);
}
