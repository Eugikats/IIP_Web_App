namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// Where a mailbox's mail is collected from (build-loop item 28). Microsoft 365 mailboxes are read through Microsoft Graph with
/// the insurer's own Microsoft Entra directory; any other mail host is read over IMAP with a mailbox login the insurer
/// provides. Either way the mail lands in the same inbound store, in the insurer's own workspace.
/// </summary>
public enum MailboxSource
{
    Graph,
    Imap,
}

/// <summary>What is needed to open one IMAP mailbox. Built from a saved mailbox for the moment of use and never stored in this form.</summary>
public sealed record ImapSettings(string Host, int Port, string Username, string Password);

public sealed record ImapMailboxMessage(
    string ExternalMessageId,
    string? InternetMessageId,
    string Sender,
    string Subject,
    string BodyPreview,
    DateTimeOffset ReceivedAt,
    IReadOnlyCollection<ImapMailboxAttachment> Attachments);

public sealed record ImapMailboxAttachment(string ExternalAttachmentId, string FileName, string ContentType, long SizeBytes);

/// <summary>The outcome of trying a mailbox login. SafeError is plain words with nothing the provider said that could carry a secret.</summary>
public sealed record ImapCheckResult(bool Success, string? SafeError);

/// <summary>
/// Talks IMAP to a mail host. Always over TLS (an insurer's mail is never read in the clear), always to a public address (a
/// workspace cannot point it at the platform's own network), and never writes anything except marking a message processed.
/// </summary>
public interface IImapMailClient
{
    /// <summary>Unread messages in the inbox, newest first, at most <paramref name="max"/>, without downloading attachments.</summary>
    Task<IReadOnlyCollection<ImapMailboxMessage>> ReadMessagesAsync(ImapSettings settings, int max, CancellationToken cancellationToken = default);

    Task<GraphAttachmentContent> DownloadAttachmentAsync(
        ImapSettings settings, string externalMessageId, string externalAttachmentId, CancellationToken cancellationToken = default);

    /// <summary>Marks the message read and, when a folder is named, moves it there (created if missing).</summary>
    Task MarkProcessedAsync(ImapSettings settings, string externalMessageId, string? folderName, CancellationToken cancellationToken = default);

    /// <summary>Opens the mailbox with these settings and reports whether it worked. Used before anything is saved.</summary>
    Task<ImapCheckResult> CheckAsync(ImapSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Collects a workspace's IMAP mailboxes into inbound messages, the way <see cref="IGraphMailProvider"/> does for Microsoft 365.</summary>
public interface IImapMailProvider
{
    Task<IReadOnlyCollection<InboundEmailMessage>> PullMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default);

    Task CompleteMessageAsync(MailboxConnection mailbox, InboundEmailMessage message, CancellationToken cancellationToken = default);
}
