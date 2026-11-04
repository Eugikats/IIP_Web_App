using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Email;

/// <summary>Collects an IMAP mailbox into inbound messages (build-loop item 28). The mailbox's password is decrypted only here,
/// for the moment of the call.</summary>
public sealed class ImapMailProvider(
    InfrastructureOptions options,
    IImapMailClient client,
    IConnectionSecretProtector protector) : IImapMailProvider
{
    public async Task<IReadOnlyCollection<InboundEmailMessage>> PullMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default)
    {
        if (mailbox.Source != MailboxSource.Imap || !mailbox.IngestionEnabled)
        {
            return [];
        }

        var messages = await client.ReadMessagesAsync(SettingsFor(mailbox, protector), Math.Clamp(options.GraphMail.MaxMessagesPerPull, 1, 50), cancellationToken);
        return messages.Select(message => ToInbound(mailbox, message)).ToArray();
    }

    public Task CompleteMessageAsync(MailboxConnection mailbox, InboundEmailMessage message, CancellationToken cancellationToken = default)
    {
        if (mailbox.Source != MailboxSource.Imap || string.IsNullOrWhiteSpace(message.ExternalMessageId))
        {
            return Task.CompletedTask;
        }

        return client.MarkProcessedAsync(SettingsFor(mailbox, protector), message.ExternalMessageId, mailbox.ProcessedFolderName, cancellationToken);
    }

    public static ImapSettings SettingsFor(MailboxConnection mailbox, IConnectionSecretProtector protector)
    {
        if (string.IsNullOrWhiteSpace(mailbox.ImapHost)
            || string.IsNullOrWhiteSpace(mailbox.ImapUsername)
            || string.IsNullOrWhiteSpace(mailbox.EncryptedImapPassword))
        {
            throw new InvalidOperationException("The IMAP mailbox has no host, username or password saved.");
        }

        return new ImapSettings(mailbox.ImapHost, mailbox.ImapPort, mailbox.ImapUsername, protector.Unprotect(mailbox.EncryptedImapPassword));
    }

    private static InboundEmailMessage ToInbound(MailboxConnection mailbox, ImapMailboxMessage message)
    {
        // The same message in the same mailbox always gets the same id, so collecting twice never duplicates it.
        var messageId = StableGuid($"{mailbox.Scope.TenantId}:{mailbox.MailboxAddress}:{message.ExternalMessageId}");
        return new InboundEmailMessage
        {
            MessageId = messageId,
            Scope = mailbox.Scope,
            MailboxConnectionId = mailbox.MailboxConnectionId,
            ExternalMessageId = message.ExternalMessageId,
            InternetMessageId = message.InternetMessageId,
            Sender = message.Sender,
            Subject = message.Subject,
            BodyPreview = SafePreview(message.BodyPreview),
            ReceivedAt = message.ReceivedAt,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments = message.Attachments
                .Select(attachment => new EmailAttachment(
                    StableGuid($"{message.ExternalMessageId}:{attachment.ExternalAttachmentId}"),
                    messageId,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.SizeBytes,
                    attachment.ExternalAttachmentId))
                .ToArray(),
        };
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        return new Guid(bytes);
    }

    private static string SafePreview(string value)
    {
        var sanitized = System.Text.RegularExpressions.Regex.Replace(value, @"\s+", " ").Trim();
        return sanitized.Length <= 280 ? sanitized : sanitized[..280];
    }
}

/// <summary>Stores the attachments of an email that arrived over IMAP on a claim.</summary>
public sealed class ImapEmailAttachmentProcessor(
    IImapMailClient client,
    IConnectionSecretProtector protector,
    EmailAttachmentDocumentBuilder builder) : IEmailAttachmentProcessor
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
            return [];
        }

        if (sourceMailbox is null || sourceMailbox.Source != MailboxSource.Imap)
        {
            throw new InvalidOperationException("IMAP email attachment processing requires the source IMAP mailbox.");
        }

        if (string.IsNullOrWhiteSpace(message.ExternalMessageId))
        {
            throw new InvalidOperationException("IMAP email attachment processing requires the message reference.");
        }

        var settings = ImapMailProvider.SettingsFor(sourceMailbox, protector);
        var documents = new List<CaseDocument>();
        foreach (var attachment in message.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.ExternalAttachmentId))
            {
                throw new InvalidOperationException("IMAP email attachment processing requires the attachment reference.");
            }

            var content = await client.DownloadAttachmentAsync(settings, message.ExternalMessageId, attachment.ExternalAttachmentId, cancellationToken);
            if (content.ContentBytes.Length == 0)
            {
                throw new InvalidOperationException("The email attachment was empty.");
            }

            documents.Add(await builder.BuildAsync(
                claimCase, attachment, content, actorUserId, "ImapEmailAttachment",
                $"imap-email-{message.MessageId:N}-{attachment.AttachmentId:N}", cancellationToken));
        }

        return documents;
    }
}
