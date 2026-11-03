using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Application.Channels;

public interface IChannelMessageIngestionService
{
    Task<bool> IngestInboundAsync(ChannelMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Persists normalized provider events once and audits metadata without message content.</summary>
public sealed class ChannelMessageIngestionService(
    IChannelMessageRepository repository,
    IAuditRepository auditRepository) : IChannelMessageIngestionService
{
    private static readonly HashSet<string> SupportedChannels = new(StringComparer.Ordinal)
    {
        ChannelValues.Instagram,
        ChannelValues.Messenger,
        ChannelValues.TikTok,
        ChannelValues.X,
        ChannelValues.Telegram,
    };

    public async Task<bool> IngestInboundAsync(ChannelMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!SupportedChannels.Contains(message.Channel)
            || message.Direction != ChannelMessageDirection.Inbound
            || string.IsNullOrWhiteSpace(message.AccountId) || message.AccountId.Length > 240
            || string.IsNullOrWhiteSpace(message.ThreadId) || message.ThreadId.Length > 320
            || string.IsNullOrWhiteSpace(message.ExternalRecipientId) || message.ExternalRecipientId.Length > 320
            || string.IsNullOrWhiteSpace(message.ProviderMessageId) || message.ProviderMessageId.Length > 320
            || string.IsNullOrWhiteSpace(message.ContentType) || message.ContentType.Length > 40
            || message.CustomerSafeBody.Length > 4096 || string.IsNullOrWhiteSpace(message.CorrelationId))
            throw new ArgumentException("The normalized inbound message is incomplete or exceeds a supported limit.", nameof(message));

        var saved = await repository.SaveInboundIfNewAsync(message, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.ChannelMessageIngested(message.Scope, message.Channel, !saved, message.CorrelationId),
            cancellationToken);
        return saved;
    }
}
