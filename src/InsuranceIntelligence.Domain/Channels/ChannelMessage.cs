using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Channels;

public enum ChannelMessageDirection { Inbound, Outbound }
public enum ChannelMessageDeliveryState { Received, Queued, ProviderAccepted, Delivered, Read, Failed, Unknown }

/// <summary>A provider-neutral, tenant-owned message. Provider payloads remain in adapters.</summary>
public sealed record ChannelMessage
{
    public required Guid MessageId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string Channel { get; init; }
    public required string AccountId { get; init; }
    public required string ThreadId { get; init; }
    public required string ExternalRecipientId { get; init; }
    public required ChannelMessageDirection Direction { get; init; }
    public required string ContentType { get; init; }
    public required string CustomerSafeBody { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? SentByUserId { get; init; }
    public required ChannelMessageDeliveryState DeliveryState { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CorrelationId { get; init; }
}

public interface IChannelMessageRepository
{
    Task<bool> SaveInboundIfNewAsync(ChannelMessage message, CancellationToken cancellationToken = default);
    Task SaveAsync(ChannelMessage message, CancellationToken cancellationToken = default);
    Task<ChannelMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string channel, string accountId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelMessage>> ListThreadAsync(
        TenantScope scope,
        string channel,
        string threadId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelMessage>> ListForExternalRecipientAsync(
        TenantScope scope, string channel, string accountId, string externalRecipientId,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChannelMessage>>([]);
}

public sealed record ChannelProviderSendResult(bool Accepted, string? ProviderMessageId, string? SafeError, bool OutcomeUnknown = false);

public interface ITelegramTextSender
{
    Task<ChannelProviderSendResult> SendAsync(string botToken, string chatId, string text, CancellationToken cancellationToken = default);
}

public interface IMetaTextSender
{
    Task<ChannelProviderSendResult> SendAsync(string accountId, string pageAccessToken, string recipientId, string text, CancellationToken cancellationToken = default);
}

public interface ITikTokTextSender
{
    Task<ChannelProviderSendResult> SendAsync(string businessId, string accessToken, string conversationId, string text, CancellationToken cancellationToken = default);
}

public interface IXDirectMessageSender
{
    Task<ChannelProviderSendResult> SendAsync(string accessToken, string recipientId, string text, CancellationToken cancellationToken = default);
}
