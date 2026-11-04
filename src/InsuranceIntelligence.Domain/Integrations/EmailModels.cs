using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

public sealed record MailboxConnection
{
    public required Guid MailboxConnectionId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string DisplayName { get; init; }
    public required string MailboxAddress { get; init; }
    public required bool IngestionEnabled { get; init; }
    public required string DefaultChannel { get; init; }
    public string? ProductLine { get; init; }
    public string? ProcessedFolderName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Where this mailbox's mail is collected from (build-loop item 28). Existing mailboxes are Microsoft 365 (Graph).</summary>
    public MailboxSource Source { get; init; } = MailboxSource.Graph;

    /// <summary>For a Microsoft 365 mailbox: the insurer's own Microsoft Entra directory (tenant) id, which the platform's app
    /// is consented in. Empty means the deployment's own directory.</summary>
    public string? EntraTenantId { get; init; }

    /// <summary>For an IMAP mailbox: the mail host, always reached over TLS.</summary>
    public string? ImapHost { get; init; }

    public int ImapPort { get; init; } = 993;

    public string? ImapUsername { get; init; }

    /// <summary>The IMAP password, encrypted at rest. Never returned to a browser and never logged.</summary>
    public string? EncryptedImapPassword { get; init; }

    /// <summary>When the last collection pass reached this mailbox, and how it went ("Collected" or "CouldNotConnect"), so a
    /// mailbox that has quietly stopped working is visible instead of looking like an empty inbox.</summary>
    public DateTimeOffset? LastCheckedAt { get; init; }

    public string? LastCheckStatus { get; init; }
}

public sealed record EmailIngestionRule(
    Guid RuleId,
    TenantScope Scope,
    string Name,
    bool Enabled,
    string MatchContains,
    string DefaultProductLine);

public sealed record InboundEmailMessage
{
    public required Guid MessageId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid MailboxConnectionId { get; init; }
    public string? ExternalMessageId { get; init; }
    public string? InternetMessageId { get; init; }
    public required string Sender { get; init; }
    public required string Subject { get; init; }
    public required string BodyPreview { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public required EmailProcessingStatus ProcessingStatus { get; init; }
    public Guid? LinkedCaseId { get; init; }
    public required IReadOnlyCollection<EmailAttachment> Attachments { get; init; }
}

public sealed record EmailAttachment(
    Guid AttachmentId,
    Guid MessageId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? ExternalAttachmentId = null);

public sealed record EmailThreadLink(Guid ThreadLinkId, TenantScope Scope, Guid CaseId, Guid MessageId, string ExternalThreadId);

public enum EmailProcessingStatus
{
    Received,
    LinkedToCase,
    CaseCreated,
    Failed
}

public sealed record EmailToCaseMatchResult(
    Guid? CaseId,
    string MatchReason,
    bool CreateNewCase,
    IdentifiedSender? IdentifiedSender = null);

public sealed record GraphMailboxMessage(
    string ExternalMessageId,
    string? InternetMessageId,
    string Sender,
    string Subject,
    string BodyPreview,
    DateTimeOffset ReceivedAt,
    IReadOnlyCollection<GraphMailboxAttachment> Attachments);

public sealed record GraphMailboxAttachment(
    string ExternalAttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    bool IsInline);

public sealed record GraphAttachmentContent(
    string FileName,
    string ContentType,
    long SizeBytes,
    byte[] ContentBytes);

public sealed record GraphMailSendRequest(
    string MailboxAddress,
    string Recipient,
    string Subject,
    string Body,
    bool SaveToSentItems,
    IReadOnlyList<InsuranceIntelligence.Domain.Communications.OutboundAttachment>? Attachments = null);

public sealed record GraphMailSendResult(string ProviderMessageId);
