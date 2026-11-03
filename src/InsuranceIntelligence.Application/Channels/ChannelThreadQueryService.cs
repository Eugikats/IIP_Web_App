using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Application.Channels;

public sealed record ChannelThreadMessageDto(
    Guid MessageId,
    string Direction,
    string Body,
    string ContentType,
    string DeliveryState,
    DateTimeOffset CreatedAt,
    Guid? CaseId,
    string? SentByUserId,
    bool IsPreview);

public sealed record ChannelThreadDto(
    string Channel,
    string ThreadId,
    string? RecipientId,
    IReadOnlyList<ChannelThreadMessageDto> Messages,
    bool CanReplyInInbox,
    string? PendingReplyDraft = null);

public interface IChannelThreadQueryService
{
    Task<ChannelThreadDto?> GetAsync(string channel, string threadId, CancellationToken cancellationToken = default);
}

/// <summary>Reads the two channels that currently have retained messages without changing their storage or send paths.</summary>
public sealed class ChannelThreadQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IWhatsAppMessageRepository whatsAppMessages,
    IChannelMessageRepository channelMessages,
    IIntegrationConnectionRepository connections,
    IConsentRepository consentRepository,
    IInboundEmailRepository inboundEmails,
    IAuditWriter auditWriter,
    ICustomerConversationRepository? customerConversations = null) : IChannelThreadQueryService
{
    public async Task<ChannelThreadDto?> GetAsync(string channel, string threadId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewChannels, cancellationToken);
        if (string.IsNullOrWhiteSpace(threadId)) return null;

        var context = tenantContextAccessor.Current;
        ChannelThreadDto? result;
        switch (channel.Trim().ToLowerInvariant())
        {
            case ChannelValues.WhatsApp:
            {
                var messages = await whatsAppMessages.ListThreadAsync(context.Scope, threadId.Trim(), cancellationToken);
                if (messages.Count == 0) return null;
                var recipientIds = messages.Select(message => message.ContactWaId).Distinct(StringComparer.Ordinal).ToArray();
                if (recipientIds.Length != 1) return null;
                var canReply = await permissionService.HasPermissionAsync(Permission.ManageCustomerCommunications, cancellationToken)
                    && await permissionService.HasPermissionAsync(Permission.SendCustomerUpdate, cancellationToken);
                string? pendingReplyDraft = null;
                if (canReply && customerConversations is not null)
                {
                    var latestTurn = (await customerConversations.ListForThreadAsync(context.Scope, threadId.Trim(), cancellationToken))
                        .OrderByDescending(turn => turn.OccurredAt)
                        .FirstOrDefault();
                    pendingReplyDraft = latestTurn?.PendingReplyDraftText;
                }

                result = new ChannelThreadDto(
                    ChannelValues.WhatsApp,
                    threadId.Trim(),
                    recipientIds[0],
                    messages.Select(message => new ChannelThreadMessageDto(
                        message.MessageId,
                        message.Direction.ToString(),
                        message.CustomerSafeBody,
                        message.ContentType.ToString(),
                        message.DeliveryState.ToString(),
                        message.CreatedAt,
                        message.CaseId,
                        message.ApprovedByUserId,
                        false)).ToArray(),
                    canReply,
                    pendingReplyDraft);
                break;
            }
            case ChannelValues.Email:
            {
                if (!Guid.TryParse(threadId, out var messageId)) return null;
                var message = await inboundEmails.GetAsync(messageId, context.Scope, cancellationToken);
                if (message is null) return null;
                result = new ChannelThreadDto(
                    ChannelValues.Email,
                    message.MessageId.ToString(),
                    null,
                    [new ChannelThreadMessageDto(
                        message.MessageId,
                        "Inbound",
                        $"{message.Subject}. {message.BodyPreview}",
                        "EmailPreview",
                        message.ProcessingStatus.ToString(),
                        message.ReceivedAt,
                        message.LinkedCaseId,
                        null,
                        true)],
                    false);
                break;
            }
            case ChannelValues.Instagram:
            case ChannelValues.Messenger:
            case ChannelValues.TikTok:
            case ChannelValues.X:
            case ChannelValues.Telegram:
            {
                var messages = await channelMessages.ListThreadAsync(context.Scope, channel.Trim().ToLowerInvariant(), threadId.Trim(), cancellationToken);
                if (messages.Count == 0) return null;
                var recipientIds = messages.Select(message => message.ExternalRecipientId).Distinct(StringComparer.Ordinal).ToArray();
                if (recipientIds.Length != 1) return null;
                result = new ChannelThreadDto(
                    channel.Trim().ToLowerInvariant(),
                    threadId.Trim(),
                    recipientIds[0],
                    messages.Select(message => new ChannelThreadMessageDto(
                        message.MessageId,
                        message.Direction.ToString(),
                        message.CustomerSafeBody,
                        message.ContentType,
                        message.DeliveryState.ToString(),
                        message.CreatedAt,
                        null,
                        message.SentByUserId,
                        false)).ToArray(),
                    (channel.Trim().ToLowerInvariant() is ChannelValues.Telegram or ChannelValues.Instagram or ChannelValues.Messenger or ChannelValues.TikTok or ChannelValues.X)
                        && await CanReplyToChannelAsync(context.Scope,
                            messages.LastOrDefault(message => message.Direction == ChannelMessageDirection.Inbound) ?? messages[0],
                            channelMessages, permissionService, connections, consentRepository, cancellationToken));
                break;
            }
            default:
                return null;
        }

        await auditWriter.WriteAsync(new AuditEvent(
            Guid.NewGuid(), context.Scope, null, null, context.UserId, ActorType.User,
            "channel.thread.viewed", DateTimeOffset.UtcNow, null, null, [], null, null,
            $"channel:{result.Channel};messages:{result.Messages.Count}", null,
            "Conversation metadata recorded; message body omitted.", Guid.NewGuid().ToString("N")), cancellationToken);
        return result;
    }

    private static async Task<bool> CanReplyToChannelAsync(
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        ChannelMessage inbound,
        IChannelMessageRepository channelMessages,
        IPermissionService permissionService,
        IIntegrationConnectionRepository connections,
        IConsentRepository consentRepository,
        CancellationToken cancellationToken)
    {
        if (inbound.Direction != ChannelMessageDirection.Inbound
            || !await permissionService.HasPermissionAsync(Permission.ManageCustomerCommunications, cancellationToken)
            || !await permissionService.HasPermissionAsync(Permission.SendCustomerUpdate, cancellationToken)) return false;
        if (inbound.Channel is ChannelValues.Instagram or ChannelValues.Messenger)
        {
            var age = DateTimeOffset.UtcNow - inbound.CreatedAt;
            if (age < TimeSpan.Zero || age > TimeSpan.FromHours(24)) return false;
        }
        if (inbound.Channel == ChannelValues.TikTok)
        {
            var age = DateTimeOffset.UtcNow - inbound.CreatedAt;
            if (age < TimeSpan.Zero || age > TimeSpan.FromHours(48)) return false;
        }
        if (inbound.Channel == ChannelValues.X)
        {
            var thread = await channelMessages.ListThreadAsync(scope, inbound.Channel, inbound.ThreadId, cancellationToken);
            var age = DateTimeOffset.UtcNow - inbound.CreatedAt;
            if (age < TimeSpan.Zero || age > TimeSpan.FromHours(24)
                || thread.Count(message => message.Direction == ChannelMessageDirection.Outbound && message.CreatedAt >= inbound.CreatedAt) >= 5) return false;
        }
        var connectionType = inbound.Channel switch
        {
            ChannelValues.Telegram => ConnectionType.Telegram,
            ChannelValues.Instagram => ConnectionType.Instagram,
            ChannelValues.Messenger => ConnectionType.Messenger,
            ChannelValues.TikTok => ConnectionType.TikTok,
            ChannelValues.X => ConnectionType.X,
            _ => (ConnectionType?)null,
        };
        if (connectionType is null) return false;
        var purpose = inbound.Channel switch
        {
            ChannelValues.Telegram => ConsentPurposes.Telegram,
            ChannelValues.Instagram => ConsentPurposes.Instagram,
            ChannelValues.Messenger => ConsentPurposes.Messenger,
            ChannelValues.TikTok => ConsentPurposes.TikTok,
            _ => ConsentPurposes.X,
        };
        var connection = await connections.GetAsync(scope, connectionType.Value, cancellationToken);
        if (connection is null || !connection.IsActive || connection.LastValidationStatus != "Success"
            || string.IsNullOrWhiteSpace(connection.LastValidationDiscoveredJson)) return false;
        var discovered = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(connection.LastValidationDiscoveredJson);
        if (discovered is null || !discovered.TryGetValue("accountId", out var accountId) || accountId != inbound.AccountId) return false;
        var consent = await consentRepository.GetCurrentAsync(scope, inbound.ExternalRecipientId, purpose, cancellationToken);
        return ConsentEnforcementGuard.IsPermitted(consent);
    }
}
