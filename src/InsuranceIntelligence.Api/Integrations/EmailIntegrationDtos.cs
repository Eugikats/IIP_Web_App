using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Integrations;

public sealed record CreateMailboxRequest(
    string DisplayName,
    string MailboxAddress,
    bool IngestionEnabled,
    string DefaultChannel,
    string? ProductLine = null,
    string? ProcessedFolderName = null,
    string? Source = null,
    string? EntraTenantId = null,
    string? ImapHost = null,
    int? ImapPort = null,
    string? ImapUsername = null,
    string? ImapPassword = null);

/// <summary>What an administrator may change about a mailbox. Where the mail comes from cannot be changed; connect a new mailbox for
/// that. A blank IMAP password keeps the saved one.</summary>
public sealed record UpdateMailboxRequest(
    string? DisplayName,
    bool? IngestionEnabled,
    string? ProductLine,
    string? ProcessedFolderName,
    string? EntraTenantId,
    string? ImapHost,
    int? ImapPort,
    string? ImapUsername,
    string? ImapPassword);

/// <summary>A mailbox as the browser sees it: never the password, only whether one is saved.</summary>
public sealed record MailboxResponse(
    Guid MailboxConnectionId,
    string DisplayName,
    string MailboxAddress,
    bool IngestionEnabled,
    string DefaultChannel,
    string? ProductLine,
    string? ProcessedFolderName,
    DateTimeOffset CreatedAt,
    string Source,
    string? EntraTenantId,
    string? ImapHost,
    int ImapPort,
    string? ImapUsername,
    bool ImapPasswordSet,
    DateTimeOffset? LastCheckedAt,
    string? LastCheckStatus)
{
    public static MailboxResponse From(InsuranceIntelligence.Domain.Integrations.MailboxConnection mailbox) =>
        new(
            mailbox.MailboxConnectionId, mailbox.DisplayName, mailbox.MailboxAddress, mailbox.IngestionEnabled, mailbox.DefaultChannel,
            mailbox.ProductLine, mailbox.ProcessedFolderName, mailbox.CreatedAt, mailbox.Source.ToString(), mailbox.EntraTenantId,
            mailbox.ImapHost, mailbox.ImapPort, mailbox.ImapUsername, !string.IsNullOrEmpty(mailbox.EncryptedImapPassword),
            mailbox.LastCheckedAt, mailbox.LastCheckStatus);
}

public sealed record ProcessEmailMessageRequest(Guid MessageId);

public sealed record EmailIntegrationHealthResponse(
    string IntegrationId,
    string DisplayName,
    string OverallStatus,
    IReadOnlyCollection<ProviderHealthItem> Providers,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);
