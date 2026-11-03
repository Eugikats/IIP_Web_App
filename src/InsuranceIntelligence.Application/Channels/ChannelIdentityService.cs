using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Channels;

public sealed record LinkChannelIdentityCommand(
    string Channel, string ThreadId, string CustomerId, bool CustomerConsentedToContext,
    string EvidenceReference, string AuditReason);

public sealed record ChannelIdentityLinkDto(
    Guid LinkId, string CustomerId, string CustomerName, string Channel, string VerificationMethod,
    string ConsentPurpose, DateTimeOffset LinkedAt, bool IsActive, bool HasActiveConsent, DateTimeOffset? RevokedAt);

public sealed record CustomerChannelTimelineEntryDto(
    Guid MessageId, string Channel, string Direction, string Body, string ContentType,
    string DeliveryState, DateTimeOffset CreatedAt, Guid? CaseId);

public sealed record CustomerChannelTimelinePageDto(
    string CustomerId, IReadOnlyList<CustomerChannelTimelineEntryDto> Entries,
    int Page, int PageSize, bool HasMore, bool SourceHistoryCapped);

public interface IChannelIdentityService
{
    Task<ChannelIdentityLinkDto> LinkAsync(LinkChannelIdentityCommand command, CancellationToken cancellationToken = default);
    Task<ChannelIdentityLinkDto> RevokeAsync(Guid linkId, string auditReason, CancellationToken cancellationToken = default);
    Task<ChannelIdentityLinkDto?> GetForThreadAsync(string channel, string threadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelIdentityLinkDto>> ListForCustomerAsync(string customerId, CancellationToken cancellationToken = default);
    Task<CustomerChannelTimelinePageDto?> GetCustomerTimelineAsync(
        string customerId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Links an inbound provider recipient to an existing workspace customer only after an accountable
/// staff member records an identity challenge and the customer's cross-channel-context consent.
/// Provider IDs are derived from retained inbound records; callers cannot supply an identity to link.
/// </summary>
public sealed class ChannelIdentityService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICustomerRepository customers,
    IWhatsAppMessageRepository whatsAppMessages,
    IChannelMessageRepository channelMessages,
    IChannelIdentityLinkRepository identityLinks,
    IWhatsAppChannelConfigurationRepository whatsAppConfiguration,
    IIntegrationConnectionRepository connections,
    IConsentRepository consents,
    IAuditWriter auditWriter) : IChannelIdentityService
{
    public async Task<ChannelIdentityLinkDto> LinkAsync(LinkChannelIdentityCommand command, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        await permissionService.RequirePermissionAsync(Permission.ViewCustomers, cancellationToken);
        var context = tenantContextAccessor.Current;
        var channel = NormalizeChannel(command.Channel);
        var threadId = Required(command.ThreadId, 320, "threadId");
        var customerId = Guid.TryParse(command.CustomerId, out var parsedCustomerId)
            ? parsedCustomerId.ToString()
            : throw new ArgumentException("Select an existing customer in this workspace.", nameof(command));
        var evidence = Required(command.EvidenceReference, 180, "evidenceReference");
        var reason = Required(command.AuditReason, 240, "auditReason");
        if (!command.CustomerConsentedToContext)
            throw new ChannelIdentityPolicyException("ConsentRequired", "Record the customer's explicit consent before linking conversation context.");

        var customer = await customers.GetAsync(customerId, context.Scope, cancellationToken);
        if (customer is null) throw new ChannelIdentityPolicyException("CustomerNotFound", "That customer is not available in this workspace.");

        var inbound = await FindInboundIdentityAsync(channel, threadId, context.Scope, cancellationToken);
        if (inbound is null) throw new ChannelIdentityPolicyException("InboundConversationRequired", "Link a customer only from a conversation with a retained inbound message.");
        var inboundIdentity = inbound.Value;
        if (!await IsCurrentAccountAsync(inboundIdentity.Channel, inboundIdentity.AccountId, context.Scope, cancellationToken))
            throw new ChannelIdentityPolicyException("AccountNotActive", "The conversation's connected account is no longer active and validated.");

        var existing = await identityLinks.FindByExternalIdentityAsync(context.Scope, channel, inboundIdentity.AccountId, inboundIdentity.ExternalRecipientId, cancellationToken);
        if (existing is { IsActive: true })
            throw new ChannelIdentityPolicyException("IdentityAlreadyLinked", "This channel identity is already linked. Review or unlink the existing customer link first.");
        if (existing is not null && !string.Equals(existing.CustomerId, customerId, StringComparison.Ordinal))
            throw new ChannelIdentityPolicyException("RelinkRequiresReview", "This identity was previously linked to another customer. A supervisor must review its history before it can be reassigned.");

        var now = DateTimeOffset.UtcNow;
        var actor = context.UserId;
        var purpose = ChannelIdentityConsentPurposes.ForChannel(channel);
        await consents.SaveAsync(new ConsentRecord
        {
            RecordId = Guid.NewGuid(), Scope = context.Scope, CustomerIdentifier = customerId,
            Basis = ConsentBasis.ExplicitOptIn, Purpose = purpose,
            Channel = channel, RecordedByUserId = actor, RecordedAt = now, CorrelationId = Guid.NewGuid().ToString("N"),
            InteractionReference = evidence,
        }, cancellationToken);

        var link = existing is null
            ? new ChannelIdentityLink
            {
                LinkId = Guid.NewGuid(), Scope = context.Scope, CustomerId = customerId,
                Channel = channel, AccountId = inboundIdentity.AccountId, ExternalRecipientId = inboundIdentity.ExternalRecipientId,
                VerificationMethod = ChannelIdentityVerificationMethod.StaffVerifiedCustomerChallenge,
                EvidenceReference = evidence, ConsentPurpose = purpose,
                LinkedByUserId = actor, LinkedAt = now,
            }
            : existing with
            {
                VerificationMethod = ChannelIdentityVerificationMethod.StaffVerifiedCustomerChallenge,
                EvidenceReference = evidence, ConsentPurpose = purpose, LinkedByUserId = actor, LinkedAt = now,
                IsActive = true, RevokedByUserId = null, RevokedAt = null,
            };

        await identityLinks.SaveAsync(link, cancellationToken);
        await WriteAuditAsync(context.Scope, actor, "channel.identity.linked", reason,
            $"link:{link.LinkId};customer:{customerId};channel:{channel};verification:{link.VerificationMethod}", cancellationToken);
        return ToDto(link, customer.FullName, true);
    }

    public async Task<ChannelIdentityLinkDto> RevokeAsync(Guid linkId, string auditReason, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var context = tenantContextAccessor.Current;
        var reason = Required(auditReason, 240, "auditReason");
        var current = await identityLinks.GetAsync(context.Scope, linkId, cancellationToken)
            ?? throw new ChannelIdentityPolicyException("IdentityLinkNotFound", "The channel identity link was not found in this workspace.");
        if (!current.IsActive) throw new ChannelIdentityPolicyException("IdentityLinkAlreadyRevoked", "This channel identity link is already revoked.");

        var now = DateTimeOffset.UtcNow;
        var revoked = current.Revoke(context.UserId, now);
        await identityLinks.SaveAsync(revoked, cancellationToken);
        await consents.SaveAsync(new ConsentRecord
        {
            RecordId = Guid.NewGuid(), Scope = context.Scope, CustomerIdentifier = current.CustomerId,
            Basis = ConsentBasis.Withdrawn, Purpose = current.ConsentPurpose, Channel = current.Channel,
            WithdrawnAt = now, RecordedByUserId = context.UserId, RecordedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N"), InteractionReference = current.LinkId.ToString("N"),
        }, cancellationToken);
        await WriteAuditAsync(context.Scope, context.UserId, "channel.identity.revoked", reason,
            $"link:{current.LinkId};customer:{current.CustomerId};channel:{current.Channel}", cancellationToken);
        var customer = await customers.GetAsync(revoked.CustomerId, context.Scope, cancellationToken);
        return ToDto(revoked, customer?.FullName ?? "Customer", false);
    }

    public async Task<ChannelIdentityLinkDto?> GetForThreadAsync(string channel, string threadId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewChannels, cancellationToken);
        var context = tenantContextAccessor.Current;
        var normalizedChannel = NormalizeChannel(channel);
        var normalizedThreadId = Required(threadId, 320, "threadId");
        var inbound = await FindInboundIdentityAsync(normalizedChannel, normalizedThreadId, context.Scope, cancellationToken);
        if (inbound is null) return null;
        var identity = inbound.Value;
        var link = await identityLinks.FindByExternalIdentityAsync(context.Scope, identity.Channel, identity.AccountId, identity.ExternalRecipientId, cancellationToken);
        if (link is not { IsActive: true }) return null;
        var customer = await customers.GetAsync(link.CustomerId, context.Scope, cancellationToken);
        if (customer is null) return null;
        var consent = await consents.GetCurrentAsync(context.Scope, link.CustomerId, link.ConsentPurpose, cancellationToken);
        await WriteAuditAsync(context.Scope, context.UserId, "channel.identity.viewed", "Viewed the verified channel identity link.",
            $"link:{link.LinkId};customer:{link.CustomerId};channel:{link.Channel}", cancellationToken);
        return ToDto(link, customer.FullName, ConsentEnforcementGuard.IsPermitted(consent));
    }

    public async Task<IReadOnlyList<ChannelIdentityLinkDto>> ListForCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewChannels, cancellationToken);
        var context = tenantContextAccessor.Current;
        if (!Guid.TryParse(customerId, out var id)) return [];
        var customer = await customers.GetAsync(id.ToString(), context.Scope, cancellationToken);
        if (customer is null) return [];
        var links = await identityLinks.ListForCustomerAsync(context.Scope, id.ToString(), cancellationToken);
        await WriteAuditAsync(context.Scope, context.UserId, "customer.channel_identities.viewed", "Viewed channel identity links for customer operations.",
            $"customer:{id};linkCount:{links.Count}", cancellationToken);
        var result = new List<ChannelIdentityLinkDto>(links.Count);
        foreach (var link in links)
        {
            var currentConsent = await consents.GetCurrentAsync(context.Scope, link.CustomerId, link.ConsentPurpose, cancellationToken);
            result.Add(ToDto(link, customer.FullName, ConsentEnforcementGuard.IsPermitted(currentConsent)));
        }
        return result;
    }

    public async Task<CustomerChannelTimelinePageDto?> GetCustomerTimelineAsync(
        string customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewCustomers, cancellationToken);
        await permissionService.RequirePermissionAsync(Permission.ViewChannels, cancellationToken);
        if (!Guid.TryParse(customerId, out var parsedCustomerId))
            throw new ArgumentException("Customer ID must be a valid identifier.", nameof(customerId));
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize >= 500)
            throw new ArgumentException("Page must be positive, page size must be between 1 and 100, and page must be within the available timeline window.", nameof(page));

        var context = tenantContextAccessor.Current;
        var normalizedCustomerId = parsedCustomerId.ToString();
        var customer = await customers.GetAsync(normalizedCustomerId, context.Scope, cancellationToken);
        if (customer is null) return null;

        var links = await identityLinks.ListForCustomerAsync(context.Scope, normalizedCustomerId, cancellationToken);
        var entries = new List<CustomerChannelTimelineEntryDto>();
        var sourceHistoryCapped = false;
        foreach (var link in links.Where(link => link.IsActive))
        {
            var consent = await consents.GetCurrentAsync(context.Scope, link.CustomerId, link.ConsentPurpose, cancellationToken);
            if (!ConsentEnforcementGuard.IsPermitted(consent)
                || !await IsCurrentAccountAsync(link.Channel, link.AccountId, context.Scope, cancellationToken)) continue;

            if (link.Channel == ChannelValues.WhatsApp)
            {
                var messages = await whatsAppMessages.ListForRecipientAsync(
                    context.Scope, link.AccountId, link.ExternalRecipientId, cancellationToken);
                sourceHistoryCapped |= messages.Count >= 500;
                entries.AddRange(messages.Select(message => new CustomerChannelTimelineEntryDto(
                    message.MessageId, link.Channel, message.Direction.ToString(), message.CustomerSafeBody,
                    message.ContentType.ToString(), message.DeliveryState.ToString(), message.CreatedAt, message.CaseId)));
            }
            else
            {
                var messages = await channelMessages.ListForExternalRecipientAsync(
                    context.Scope, link.Channel, link.AccountId, link.ExternalRecipientId, cancellationToken);
                sourceHistoryCapped |= messages.Count >= 500;
                entries.AddRange(messages.Select(message => new CustomerChannelTimelineEntryDto(
                    message.MessageId, link.Channel, message.Direction.ToString(), message.CustomerSafeBody,
                    message.ContentType, message.DeliveryState.ToString(), message.CreatedAt, null)));
            }
        }

        var ordered = entries.OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.MessageId).ToArray();
        var offset = (page - 1) * pageSize;
        var pageEntries = ordered.Skip(offset).Take(pageSize).ToArray();
        var hasMore = ordered.Length > offset + pageEntries.Length;
        await WriteAuditAsync(context.Scope, context.UserId, "customer.channel_timeline.viewed",
            "Viewed the consented cross-channel customer timeline.",
            $"customer:{normalizedCustomerId};page:{page};pageSize:{pageSize};returned:{pageEntries.Length};hasMore:{hasMore};historyCapped:{sourceHistoryCapped}",
            cancellationToken);
        return new CustomerChannelTimelinePageDto(normalizedCustomerId, pageEntries, page, pageSize, hasMore, sourceHistoryCapped);
    }

    private async Task<(string Channel, string AccountId, string ExternalRecipientId)?> FindInboundIdentityAsync(
        string channel, string threadId, TenantScope scope, CancellationToken cancellationToken)
    {
        if (channel == ChannelValues.WhatsApp)
        {
            var messages = await whatsAppMessages.ListThreadAsync(scope, threadId, cancellationToken);
            var inbound = messages.Where(message => message.Direction == WhatsAppMessageDirection.Inbound)
                .Select(message => (message.BusinessPhoneNumberId, message.ContactWaId)).Distinct().ToArray();
            return inbound.Length == 1 ? (channel, inbound[0].BusinessPhoneNumberId, inbound[0].ContactWaId) : null;
        }

        var records = await channelMessages.ListThreadAsync(scope, channel, threadId, cancellationToken);
        var identities = records.Where(message => message.Direction == ChannelMessageDirection.Inbound)
            .Select(message => (message.AccountId, message.ExternalRecipientId)).Distinct().ToArray();
        return identities.Length == 1 ? (channel, identities[0].AccountId, identities[0].ExternalRecipientId) : null;
    }

    private async Task<bool> IsCurrentAccountAsync(string channel, string accountId, TenantScope scope, CancellationToken cancellationToken)
    {
        if (channel == ChannelValues.WhatsApp)
        {
            var config = await whatsAppConfiguration.GetAsync(scope, cancellationToken);
            return config is { IsActive: true, LastValidationStatus: "Success" }
                && string.Equals(config.PhoneNumberId, accountId, StringComparison.Ordinal);
        }

        var type = channel switch
        {
            ChannelValues.Instagram => ConnectionType.Instagram,
            ChannelValues.Messenger => ConnectionType.Messenger,
            ChannelValues.TikTok => ConnectionType.TikTok,
            ChannelValues.X => ConnectionType.X,
            ChannelValues.Telegram => ConnectionType.Telegram,
            _ => (ConnectionType?)null,
        };
        if (type is null) return false;
        var connection = await connections.GetAsync(scope, type.Value, cancellationToken);
        if (connection is not { IsActive: true, LastValidationStatus: "Success" }
            || string.IsNullOrWhiteSpace(connection.LastValidationDiscoveredJson)) return false;
        try
        {
            var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(connection.LastValidationDiscoveredJson);
            return values is not null && values.TryGetValue("accountId", out var configuredAccount)
                && string.Equals(configuredAccount, accountId, StringComparison.Ordinal);
        }
        catch (System.Text.Json.JsonException) { return false; }
    }

    private async Task WriteAuditAsync(TenantScope scope, string actor, string action, string reason, string metadata, CancellationToken cancellationToken) =>
        await auditWriter.WriteAsync(new AuditEvent(Guid.NewGuid(), scope, null, null, actor, ActorType.User, action,
            DateTimeOffset.UtcNow, null, null, [], "Admin", null, metadata, null, reason, Guid.NewGuid().ToString("N")), cancellationToken);

    private static string NormalizeChannel(string value)
    {
        var channel = value?.Trim().ToLowerInvariant();
        return channel is ChannelValues.WhatsApp or ChannelValues.Instagram or ChannelValues.Messenger
            or ChannelValues.TikTok or ChannelValues.X or ChannelValues.Telegram
            ? channel
            : throw new ArgumentException("Choose a supported messaging channel.", nameof(value));
    }

    private static string Required(string? value, int maximumLength, string field)
    {
        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{field} is required and must be no longer than {maximumLength} characters.", field);
    }

    private static ChannelIdentityLinkDto ToDto(ChannelIdentityLink link, string customerName, bool hasActiveConsent) => new(
        link.LinkId, link.CustomerId, customerName, link.Channel, link.VerificationMethod.ToString(), link.ConsentPurpose,
        link.LinkedAt, link.IsActive, hasActiveConsent, link.RevokedAt);
}

public sealed class ChannelIdentityPolicyException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
