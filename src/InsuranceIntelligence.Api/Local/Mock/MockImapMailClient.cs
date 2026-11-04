using System.Collections.Concurrent;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Local.Mock;

public sealed record MockMailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record MockMail(
    string ExternalId,
    string From,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<MockMailAttachment> Attachments)
{
    public bool Seen { get; set; }
    public string? Folder { get; set; }
}

/// <summary>
/// A pretend mail host for a developer runtime: mailboxes that live in memory and are filled through
/// <c>/api/v1/dev/mock-mailbox/deliver</c>. It stands in for the insurer's real IMAP server so the whole path (connect a mailbox,
/// collect, process into a claim, file the attachments) can be tried with no real mailbox and no credential. Like a real host
/// it is shared by whoever connects to the same host and username; what each workspace collects from it is stored in that
/// workspace's own scope only.
/// </summary>
public sealed class MockMailStore
{
    public const string RefusedPassword = "wrong-password";
    private readonly ConcurrentDictionary<string, List<MockMail>> boxes = new(StringComparer.OrdinalIgnoreCase);

    public static string Key(string host, string username) => $"{host.Trim()}|{username.Trim()}";

    public MockMail Deliver(string host, string username, string from, string subject, string body, IReadOnlyList<MockMailAttachment> attachments)
    {
        var box = boxes.GetOrAdd(Key(host, username), _ => []);
        lock (box)
        {
            var mail = new MockMail($"1:{box.Count + 1}", from, subject, body, DateTimeOffset.UtcNow, attachments);
            box.Add(mail);
            return mail;
        }
    }

    public IReadOnlyList<MockMail> Unread(string host, string username)
    {
        if (!boxes.TryGetValue(Key(host, username), out var box))
        {
            return [];
        }

        lock (box)
        {
            return box.Where(mail => !mail.Seen).OrderByDescending(mail => mail.ReceivedAt).ToArray();
        }
    }

    public MockMail? Find(string host, string username, string externalId)
    {
        if (!boxes.TryGetValue(Key(host, username), out var box))
        {
            return null;
        }

        lock (box)
        {
            return box.FirstOrDefault(mail => mail.ExternalId == externalId);
        }
    }
}

public sealed class MockImapMailClient(MockMailStore store) : IImapMailClient
{
    public Task<IReadOnlyCollection<ImapMailboxMessage>> ReadMessagesAsync(ImapSettings settings, int max, CancellationToken cancellationToken = default)
    {
        Authenticate(settings);
        IReadOnlyCollection<ImapMailboxMessage> messages = store.Unread(settings.Host, settings.Username)
            .Take(Math.Clamp(max, 1, 50))
            .Select(mail => new ImapMailboxMessage(
                mail.ExternalId,
                $"<{mail.ExternalId}@mock.iip.local>",
                mail.From,
                mail.Subject,
                mail.Body,
                mail.ReceivedAt,
                mail.Attachments.Select((file, index) => new ImapMailboxAttachment((index + 1).ToString(), file.FileName, file.ContentType, file.Content.LongLength)).ToArray()))
            .ToArray();
        return Task.FromResult(messages);
    }

    public Task<GraphAttachmentContent> DownloadAttachmentAsync(ImapSettings settings, string externalMessageId, string externalAttachmentId, CancellationToken cancellationToken = default)
    {
        Authenticate(settings);
        var mail = store.Find(settings.Host, settings.Username, externalMessageId)
            ?? throw new InvalidOperationException("The message is no longer in the mailbox.");
        if (!int.TryParse(externalAttachmentId, out var number) || number < 1 || number > mail.Attachments.Count)
        {
            throw new InvalidOperationException("The attachment is no longer in the message.");
        }

        var file = mail.Attachments[number - 1];
        return Task.FromResult(new GraphAttachmentContent(file.FileName, file.ContentType, file.Content.LongLength, file.Content));
    }

    public Task MarkProcessedAsync(ImapSettings settings, string externalMessageId, string? folderName, CancellationToken cancellationToken = default)
    {
        Authenticate(settings);
        var mail = store.Find(settings.Host, settings.Username, externalMessageId)
            ?? throw new InvalidOperationException("The message is no longer in the mailbox.");
        mail.Seen = true;
        mail.Folder = string.IsNullOrWhiteSpace(folderName) ? null : folderName.Trim();
        return Task.CompletedTask;
    }

    public Task<ImapCheckResult> CheckAsync(ImapSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.Host.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ImapCheckResult(false, "The mail host could not be reached with those details. Check the host, the port and that IMAP is switched on for the mailbox."));
        }

        return Task.FromResult(settings.Password == MockMailStore.RefusedPassword
            ? new ImapCheckResult(false, "The mail host refused the username or password.")
            : new ImapCheckResult(true, null));
    }

    private static void Authenticate(ImapSettings settings)
    {
        if (settings.Password == MockMailStore.RefusedPassword)
        {
            throw new System.Security.Authentication.AuthenticationException("The mail host refused the username or password.");
        }
    }
}

public sealed record MockDeliverRequest(
    string? Host,
    string? Username,
    string? From,
    string? Subject,
    string? Body,
    IReadOnlyList<MockDeliverAttachment>? Attachments);

public sealed record MockDeliverAttachment(string? FileName, string? ContentType, string? Text);

/// <summary>Developer-runtime-only: puts a message into a pretend mailbox. Mapped only with development diagnostics.</summary>
public static class MockMailboxEndpoints
{
    public static IResult Deliver(MockDeliverRequest request, MockMailStore? store)
    {
        if (store is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured", detail: "The Mock email provider is not active.");
        }

        if (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.Username))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["mailbox"] = ["Name the host and username of the pretend mailbox."] });
        }

        var attachments = (request.Attachments ?? [])
            .Select(file => new MockMailAttachment(
                string.IsNullOrWhiteSpace(file.FileName) ? "attachment.txt" : Path.GetFileName(file.FileName),
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType,
                System.Text.Encoding.UTF8.GetBytes(file.Text ?? string.Empty)))
            .ToArray();
        var mail = store.Deliver(request.Host, request.Username, request.From ?? "customer@example.test", request.Subject ?? "(no subject)", request.Body ?? string.Empty, attachments);
        return Results.Ok(new { delivered = true, mail.ExternalId });
    }
}
