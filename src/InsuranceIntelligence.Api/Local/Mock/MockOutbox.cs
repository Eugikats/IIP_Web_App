using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>One message the Mock communication adapters "sent". Nothing leaves the process.</summary>
public sealed record MockOutboxMessage(
    Guid MessageId,
    string TenantId,
    string WorkspaceId,
    CommunicationChannel Channel,
    string Recipient,
    string Subject,
    string Body,
    IReadOnlyList<MockOutboxAttachment> Attachments,
    DateTimeOffset SentAt);

public sealed record MockOutboxAttachment(string FileName, string ContentType, long SizeBytes);

/// <summary>
/// In-memory record of everything the Mock communication adapters were asked to send, so a person
/// or an end-to-end test can read what a customer or insurer would have received. Bounded, and
/// scoped by tenant on every read so one tenant never sees another tenant's mock mail.
/// </summary>
public sealed class MockOutbox
{
    private const int MaxMessages = 500;
    private readonly ConcurrentQueue<MockOutboxMessage> messages = new();

    public MockOutboxMessage Add(
        string tenantId,
        string workspaceId,
        CommunicationChannel channel,
        string recipient,
        string subject,
        string body,
        IReadOnlyList<MockOutboxAttachment>? attachments = null)
    {
        var message = new MockOutboxMessage(
            Guid.NewGuid(), tenantId, workspaceId, channel, recipient, subject, body,
            attachments ?? Array.Empty<MockOutboxAttachment>(), DateTimeOffset.UtcNow);
        messages.Enqueue(message);
        while (messages.Count > MaxMessages && messages.TryDequeue(out _))
        {
        }

        return message;
    }

    public IReadOnlyList<MockOutboxMessage> ListFor(string tenantId, string workspaceId) =>
        messages
            .Where(message => string.Equals(message.TenantId, tenantId, StringComparison.Ordinal)
                && string.Equals(message.WorkspaceId, workspaceId, StringComparison.Ordinal))
            .OrderByDescending(message => message.SentAt)
            .ToArray();

    public void Clear(string tenantId, string workspaceId)
    {
        var keep = messages
            .Where(message => !(string.Equals(message.TenantId, tenantId, StringComparison.Ordinal)
                && string.Equals(message.WorkspaceId, workspaceId, StringComparison.Ordinal)))
            .ToArray();
        while (messages.TryDequeue(out _))
        {
        }

        foreach (var message in keep)
        {
            messages.Enqueue(message);
        }
    }
}

/// <summary>Mock email and SMS sending: records into <see cref="MockOutbox"/> and reports Sent.
/// Registered only under a developer runtime when the communication provider is set to Mock.</summary>
public sealed class MockCommunicationSendingProvider(MockOutbox outbox) : ICommunicationSendingProvider
{
    public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Record(request, CommunicationChannel.Email));

    public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Record(request, CommunicationChannel.SMS));

    private CommunicationSendResult Record(CommunicationSendRequest request, CommunicationChannel channel)
    {
        if (string.IsNullOrWhiteSpace(request.Recipient))
        {
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, "No recipient was supplied.");
        }

        var message = outbox.Add(
            request.Scope.TenantId, request.Scope.WorkspaceId, channel, request.Recipient, string.Empty, request.MessageBody);
        return new CommunicationSendResult("Sent", $"mock-{message.MessageId:N}", null);
    }
}
