using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Channels;

public sealed record SendChannelMessageCommand(string Channel, string ThreadId, string Body, string IdempotencyKey, bool HumanApproved);
public sealed record ChannelMessageSendResult(Guid MessageId, string DeliveryState, string? SafeError);

public interface IChannelMessageSendService
{
    Task<ChannelMessageSendResult> SendAsync(SendChannelMessageCommand command, CancellationToken cancellationToken = default);
}

public sealed class ChannelMessagePolicyException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

/// <summary>Human approved, consent checked replies. Unknown provider outcomes are retained
/// and block another send so a timeout cannot create a duplicate customer message.</summary>
public sealed class ChannelMessageSendService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IChannelMessageRepository messages,
    IIntegrationConnectionRepository connections,
    IConnectionSecretProtector secretProtector,
    ITelegramTextSender telegramSender,
    IMetaTextSender metaSender,
    ITikTokTextSender tikTokSender,
    IXDirectMessageSender xSender,
    IConsentRepository consentRepository,
    IAuditWriter auditWriter) : IChannelMessageSendService
{
    private static readonly string[] RestrictedTerms = ["raw prompt", "system message", "internal note", "fraud flag", "reserve", "hidden risk", "secret"];

    public async Task<ChannelMessageSendResult> SendAsync(SendChannelMessageCommand command, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        await permissionService.RequirePermissionAsync(Permission.SendCustomerUpdate, cancellationToken);
        var channel = command.Channel.Trim().ToLowerInvariant();
        if (channel is not (ChannelValues.Telegram or ChannelValues.Instagram or ChannelValues.Messenger or ChannelValues.TikTok or ChannelValues.X))
            throw new ChannelMessagePolicyException("ChannelUnavailable", "Replies are not configured for this channel.");
        if (!command.HumanApproved) throw new ChannelMessagePolicyException("ApprovalRequired", "Review and approve the exact message before sending.");
        if (string.IsNullOrWhiteSpace(command.ThreadId) || command.ThreadId.Length > 128)
            throw new ChannelMessagePolicyException("ThreadRequired", "Select a valid conversation.");
        var maxLength = channel == ChannelValues.Telegram ? 4096 : channel == ChannelValues.X ? 10000 : 1000;
        if (string.IsNullOrWhiteSpace(command.Body) || command.Body.Length > maxLength)
            throw new ChannelMessagePolicyException("MessageInvalid", $"Messages must contain 1 to {maxLength:N0} characters for this channel.");
        if (RestrictedTerms.Any(term => command.Body.Contains(term, StringComparison.OrdinalIgnoreCase)))
            throw new ChannelMessagePolicyException("RestrictedContent", "Remove internal or restricted content before sending.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128)
            throw new ChannelMessagePolicyException("IdempotencyKeyRequired", "A valid send key is required.");

        var context = tenantContextAccessor.Current;
        var thread = await messages.ListThreadAsync(context.Scope, channel, command.ThreadId.Trim(), cancellationToken);
        var inbound = thread.Where(item => item.Direction == ChannelMessageDirection.Inbound)
            .OrderByDescending(item => item.CreatedAt).FirstOrDefault();
        if (inbound is null || thread.Any(item => item.ExternalRecipientId != inbound.ExternalRecipientId || item.AccountId != inbound.AccountId))
            throw new ChannelMessagePolicyException("RecipientNotVerified", "The recipient has not contacted this connected account on this conversation.");
        var inboundAge = DateTimeOffset.UtcNow - inbound.CreatedAt;
        if ((channel is ChannelValues.Instagram or ChannelValues.Messenger)
            && (inboundAge < TimeSpan.Zero || inboundAge > TimeSpan.FromHours(24)))
            throw new ChannelMessagePolicyException("MessagingWindowExpired", "Meta allows a reply only within 24 hours of the customer's latest message. Wait for a new message before replying.");
        if (channel == ChannelValues.TikTok && (inboundAge < TimeSpan.Zero || inboundAge > TimeSpan.FromHours(48)))
            throw new ChannelMessagePolicyException("MessagingWindowExpired", "TikTok Business Messaging only allows replies within its customer conversation window.");
        if (channel == ChannelValues.TikTok && thread.Count(message => message.Direction == ChannelMessageDirection.Outbound && message.CreatedAt >= inbound.CreatedAt) >= 10)
            throw new ChannelMessagePolicyException("MessagingLimitReached", "TikTok allows up to ten replies in a 48-hour customer conversation window. Wait for the customer to message again.");
        if (channel == ChannelValues.X)
        {
            var xOutboundCount = thread.Count(message => message.Direction == ChannelMessageDirection.Outbound && message.CreatedAt >= inbound.CreatedAt && message.CreatedAt <= DateTimeOffset.UtcNow);
            if (inboundAge < TimeSpan.Zero || inboundAge > TimeSpan.FromHours(24) || xOutboundCount >= 5)
                throw new ChannelMessagePolicyException("MessagingWindowExpired", "X allows up to five replies within 24 hours of the customer's latest direct message.");
        }
        var existing = await messages.FindByIdempotencyKeyAsync(context.Scope, channel, inbound.AccountId, command.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            if (existing.ThreadId != command.ThreadId.Trim() || existing.CustomerSafeBody != command.Body.Trim())
                throw new ChannelMessagePolicyException("IdempotencyConflict", "That send key is already bound to a different message.");
            return Result(existing);
        }
        if (thread.Any(item => item.Direction == ChannelMessageDirection.Outbound
            && item.DeliveryState is ChannelMessageDeliveryState.Queued or ChannelMessageDeliveryState.Unknown))
            throw new ChannelMessagePolicyException("ReconciliationRequired", "A previous send has an unknown outcome. Check it before sending again.");

        var purpose = channel switch
        {
            ChannelValues.Telegram => ConsentPurposes.Telegram,
            ChannelValues.Instagram => ConsentPurposes.Instagram,
            ChannelValues.Messenger => ConsentPurposes.Messenger,
            ChannelValues.TikTok => ConsentPurposes.TikTok,
            _ => ConsentPurposes.X,
        };
        var consent = await consentRepository.GetCurrentAsync(context.Scope, inbound.ExternalRecipientId, purpose, cancellationToken);
        if (!ConsentEnforcementGuard.IsPermitted(consent))
            throw new ChannelMessagePolicyException("ConsentRequired", "The customer must start a Telegram conversation before a reply can be sent.");

        var connectionType = channel switch
        {
            ChannelValues.Telegram => ConnectionType.Telegram,
            ChannelValues.Instagram => ConnectionType.Instagram,
            ChannelValues.Messenger => ConnectionType.Messenger,
            ChannelValues.TikTok => ConnectionType.TikTok,
            _ => ConnectionType.X,
        };
        var connection = await connections.GetAsync(context.Scope, connectionType, cancellationToken);
        if (connection is null || !connection.IsActive || connection.LastValidationStatus != "Success")
            throw new ChannelMessagePolicyException("ConfigurationRequired", "This channel is not connected and validated for this workspace.");
        var discovered = string.IsNullOrWhiteSpace(connection.LastValidationDiscoveredJson)
            ? new Dictionary<string, string>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(connection.LastValidationDiscoveredJson)
                ?? new Dictionary<string, string>();
        if (!discovered.TryGetValue("accountId", out var accountId) || accountId != inbound.AccountId)
            throw new ChannelMessagePolicyException("ConfigurationChanged", "This conversation belongs to an account that is no longer connected.");
        var settings = ConnectionSettingsCodec.Decode(secretProtector, connection);
        var tokenKey = channel == ChannelValues.Telegram ? "botToken" : channel == ChannelValues.Messenger ? "pageAccessToken" : "accessToken";
        if (!settings.TryGetValue(tokenKey, out var token) || string.IsNullOrWhiteSpace(token))
            throw new ChannelMessagePolicyException("ConfigurationRequired", "Channel credentials are not configured.");

        var now = DateTimeOffset.UtcNow;
        var pending = new ChannelMessage
        {
            MessageId = Guid.NewGuid(), Scope = context.Scope, Channel = channel, AccountId = inbound.AccountId,
            ThreadId = command.ThreadId.Trim(), ExternalRecipientId = inbound.ExternalRecipientId,
            Direction = ChannelMessageDirection.Outbound, ContentType = "Text", CustomerSafeBody = command.Body.Trim(),
            IdempotencyKey = command.IdempotencyKey.Trim(), SentByUserId = context.UserId,
            DeliveryState = ChannelMessageDeliveryState.Queued, CreatedAt = now, UpdatedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N"),
        };
        await messages.SaveAsync(pending, cancellationToken);
        ChannelProviderSendResult providerResult;
        try
        {
            providerResult = channel == ChannelValues.Telegram
                ? await telegramSender.SendAsync(token, pending.ExternalRecipientId, pending.CustomerSafeBody, cancellationToken)
                : channel is ChannelValues.Instagram or ChannelValues.Messenger
                    ? await metaSender.SendAsync(inbound.AccountId, token, pending.ExternalRecipientId, pending.CustomerSafeBody, cancellationToken)
                    : channel == ChannelValues.TikTok
                        ? await tikTokSender.SendAsync(inbound.AccountId, token, pending.ThreadId, pending.CustomerSafeBody, cancellationToken)
                        : await xSender.SendAsync(settings.GetValueOrDefault("accessToken", string.Empty),
                            pending.ExternalRecipientId, pending.CustomerSafeBody, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await messages.SaveAsync(pending with { DeliveryState = ChannelMessageDeliveryState.Unknown, UpdatedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            providerResult = new ChannelProviderSendResult(false, null, "The provider did not confirm the send. Check the conversation before retrying.", true);
        }
        var updated = pending with
        {
            ProviderMessageId = providerResult.ProviderMessageId,
            DeliveryState = providerResult.Accepted ? ChannelMessageDeliveryState.ProviderAccepted
                : providerResult.OutcomeUnknown ? ChannelMessageDeliveryState.Unknown : ChannelMessageDeliveryState.Failed,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await messages.SaveAsync(updated, cancellationToken);
        await auditWriter.WriteAsync(new AuditEvent(
            Guid.NewGuid(), context.Scope, null, null, context.UserId, ActorType.User, "channel.message.send_attempted",
            DateTimeOffset.UtcNow, null, null, [], channel, null, updated.DeliveryState.ToString(), null,
            "Human approved customer message metadata recorded; body omitted.", updated.CorrelationId), cancellationToken);
        return new ChannelMessageSendResult(updated.MessageId, updated.DeliveryState.ToString(), providerResult.SafeError);
    }

    private static ChannelMessageSendResult Result(ChannelMessage message) => new(message.MessageId, message.DeliveryState.ToString(),
        message.DeliveryState is ChannelMessageDeliveryState.Failed or ChannelMessageDeliveryState.Unknown
            ? "The previous send attempt did not complete. Check the conversation before trying again." : null);
}
