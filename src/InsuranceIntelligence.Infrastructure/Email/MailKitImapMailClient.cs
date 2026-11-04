using System.Net;
using System.Net.Sockets;
using InsuranceIntelligence.Domain.Integrations;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace InsuranceIntelligence.Infrastructure.Email;

/// <summary>
/// The real IMAP client (MailKit, MIT licence). Always TLS: port 993 is wrapped from the first byte, any other port must upgrade
/// with STARTTLS or the connection is refused, so mail and passwords are never sent in the clear. Certificates are validated
/// normally. The connection is made to an address <see cref="ImapEndpointPolicy"/> has already approved. Nothing here logs a
/// password, a message body or a host's own error text.
/// </summary>
public sealed class MailKitImapMailClient(ImapEndpointPolicy endpointPolicy, ILogger<MailKitImapMailClient> logger) : IImapMailClient
{
    private const int TimeoutMilliseconds = 30_000;
    public const long MaxAttachmentBytes = 25L * 1024 * 1024;

    public async Task<IReadOnlyCollection<ImapMailboxMessage>> ReadMessagesAsync(ImapSettings settings, int max, CancellationToken cancellationToken = default)
    {
        using var client = await ConnectAsync(settings, cancellationToken);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        var unread = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
        var newest = unread.OrderByDescending(uid => uid.Id).Take(Math.Clamp(max, 1, 50)).ToList();
        if (newest.Count == 0)
        {
            await client.DisconnectAsync(true, cancellationToken);
            return [];
        }

        var summaries = await inbox.FetchAsync(
            newest,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure | MessageSummaryItems.InternalDate,
            cancellationToken);
        var result = new List<ImapMailboxMessage>();
        foreach (var summary in summaries.OrderByDescending(item => item.UniqueId.Id))
        {
            var preview = await PreviewAsync(inbox, summary, cancellationToken);
            var attachments = (summary.Attachments ?? [])
                .Select((part, index) => new ImapMailboxAttachment(
                    part.PartSpecifier,
                    string.IsNullOrWhiteSpace(part.FileName) ? $"attachment-{index + 1}" : part.FileName,
                    part.ContentType.MimeType,
                    part.Octets))
                .ToArray();
            result.Add(new ImapMailboxMessage(
                ExternalId(inbox, summary.UniqueId),
                summary.Envelope?.MessageId,
                summary.Envelope?.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown@example.invalid",
                string.IsNullOrWhiteSpace(summary.Envelope?.Subject) ? "(no subject)" : summary.Envelope!.Subject,
                preview,
                summary.InternalDate ?? summary.Envelope?.Date ?? DateTimeOffset.UtcNow,
                attachments));
        }

        await client.DisconnectAsync(true, cancellationToken);
        return result;
    }

    public async Task<GraphAttachmentContent> DownloadAttachmentAsync(
        ImapSettings settings, string externalMessageId, string externalAttachmentId, CancellationToken cancellationToken = default)
    {
        using var client = await ConnectAsync(settings, cancellationToken);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        var uid = ParseUid(inbox, externalMessageId);
        var summary = (await inbox.FetchAsync([uid], MessageSummaryItems.BodyStructure, cancellationToken)).FirstOrDefault()
            ?? throw new InvalidOperationException("The message is no longer in the mailbox.");
        var part = (summary.Attachments ?? []).FirstOrDefault(item => item.PartSpecifier == externalAttachmentId)
            ?? throw new InvalidOperationException("The attachment is no longer in the message.");
        if (part.Octets > MaxAttachmentBytes)
        {
            throw new InvalidOperationException("The attachment is larger than the 25 MB limit.");
        }

        var entity = await inbox.GetBodyPartAsync(uid, part, cancellationToken);
        if (entity is not MimePart mime || mime.Content is null)
        {
            throw new InvalidOperationException("The attachment has no file content.");
        }

        using var memory = new MemoryStream();
        await mime.Content.DecodeToAsync(memory, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        var bytes = memory.ToArray();
        return new GraphAttachmentContent(
            string.IsNullOrWhiteSpace(part.FileName) ? "attachment" : part.FileName,
            part.ContentType.MimeType,
            bytes.LongLength,
            bytes);
    }

    public async Task MarkProcessedAsync(ImapSettings settings, string externalMessageId, string? folderName, CancellationToken cancellationToken = default)
    {
        using var client = await ConnectAsync(settings, cancellationToken);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        var uid = ParseUid(inbox, externalMessageId);
        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, cancellationToken);
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            var root = await client.GetFolderAsync(client.PersonalNamespaces[0].Path, cancellationToken);
            IMailFolder? destination = null;
            try
            {
                destination = await root.GetSubfolderAsync(folderName.Trim(), cancellationToken);
            }
            catch (FolderNotFoundException)
            {
            }

            destination ??= await root.CreateAsync(folderName.Trim(), isMessageFolder: true, cancellationToken)
                ?? throw new InvalidOperationException("The folder for processed mail could not be created.");
            await inbox.MoveToAsync(uid, destination, cancellationToken);
        }

        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task<ImapCheckResult> CheckAsync(ImapSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = await ConnectAsync(settings, cancellationToken);
            await client.Inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return new ImapCheckResult(true, null);
        }
        catch (ImapEndpointRefusedException refused)
        {
            return new ImapCheckResult(false, refused.Message);
        }
        catch (AuthenticationException)
        {
            return new ImapCheckResult(false, "The mail host refused the username or password.");
        }
        catch (SslHandshakeException)
        {
            return new ImapCheckResult(false, "The mail host's security certificate could not be trusted.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogInformation("An IMAP check failed ({ErrorType}).", exception.GetType().Name);
            return new ImapCheckResult(false, "The mail host could not be reached with those details. Check the host, the port and that IMAP is switched on for the mailbox.");
        }
    }

    private async Task<ImapClient> ConnectAsync(ImapSettings settings, CancellationToken cancellationToken)
    {
        var addresses = await endpointPolicy.ResolveAsync(settings.Host, cancellationToken);
        var client = new ImapClient { Timeout = TimeoutMilliseconds };
        try
        {
            Socket? socket = null;
            Exception? last = null;
            foreach (var address in addresses)
            {
                var attempt = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeoutMilliseconds);
                    await attempt.ConnectAsync(new IPEndPoint(address, settings.Port), timeout.Token);
                    socket = attempt;
                    break;
                }
                catch (Exception exception) when (exception is SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    last = exception;
                    attempt.Dispose();
                }
            }

            if (socket is null)
            {
                throw new IOException("No address of the mail host accepted the connection.", last);
            }

            // Port 993 is TLS from the first byte; any other port must offer STARTTLS or the connection is refused.
            await client.ConnectAsync(
                socket,
                settings.Host.Trim(),
                settings.Port,
                settings.Port == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                cancellationToken);
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static string ExternalId(IMailFolder folder, UniqueId uid) => $"{folder.UidValidity}:{uid.Id}";

    private static UniqueId ParseUid(IMailFolder folder, string externalId)
    {
        var parts = externalId.Split(':');
        if (parts.Length != 2 || !uint.TryParse(parts[0], out var validity) || !uint.TryParse(parts[1], out var id))
        {
            throw new InvalidOperationException("The message reference is not valid.");
        }

        // A mailbox that was rebuilt gives every message a new number; the old reference no longer points at that message.
        if (validity != folder.UidValidity)
        {
            throw new InvalidOperationException("The mailbox was rebuilt since this message was collected.");
        }

        return new UniqueId(id);
    }

    private static async Task<string> PreviewAsync(IMailFolder inbox, IMessageSummary summary, CancellationToken cancellationToken)
    {
        try
        {
            if (summary.TextBody is null)
            {
                return string.Empty;
            }

            var entity = await inbox.GetBodyPartAsync(summary.UniqueId, summary.TextBody, cancellationToken);
            return entity is TextPart text ? text.Text : string.Empty;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A preview is a convenience; a body that cannot be read must not hide the message.
            return string.Empty;
        }
    }
}
