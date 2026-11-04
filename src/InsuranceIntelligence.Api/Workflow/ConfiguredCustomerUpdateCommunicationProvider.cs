using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Workflow;

/// <summary>
/// Sends a customer update on its channel: email and text message through the configured sending provider,
/// and WhatsApp through the workspace's own connected number (build-loop item 21). WhatsApp has two rules the
/// others do not: the customer must have a recorded, unwithdrawn consent, and once the customer's last message
/// is more than 24 hours old only a template Meta has approved may be sent. A send that cannot go out says
/// why (ConsentRequired, TemplateRequired, ConfigurationRequired); it is never reported as sent.
/// </summary>
public sealed class ConfiguredCustomerUpdateCommunicationProvider(
    ICommunicationSendingProvider communicationSendingProvider,
    IWhatsAppSenderResolver? whatsAppSenderResolver = null,
    IWhatsAppMessageRepository? whatsAppMessageRepository = null,
    IConsentRepository? consentRepository = null,
    ITenantSettingsRepository? settingsRepository = null) : ICommunicationProvider
{
    private static readonly TimeSpan ServiceWindow = TimeSpan.FromHours(24);

    public async Task<CustomerUpdateDraft> SendAsync(
        CustomerUpdateDraft update,
        string sentByUserId,
        string? recipient = null,
        CancellationToken cancellationToken = default)
    {
        if (update.Channel == CommunicationChannel.WhatsApp)
        {
            return await SendWhatsAppAsync(update, sentByUserId, recipient, cancellationToken);
        }

        if ((update.Channel is CommunicationChannel.Email or CommunicationChannel.SMS)
            && string.IsNullOrWhiteSpace(recipient))
        {
            return Failed(update, sentByUserId, "ConfigurationRequired");
        }

        var result = update.Channel switch
        {
            CommunicationChannel.Email => await communicationSendingProvider.SendEmailAsync(ToRequest(update, sentByUserId, recipient), cancellationToken),
            CommunicationChannel.SMS => await communicationSendingProvider.SendSmsAsync(ToRequest(update, sentByUserId, recipient), cancellationToken),
            _ => new CommunicationSendResult(
                "ConfigurationRequired",
                string.Empty,
                $"{update.Channel} customer-update sending is not configured.")
        };

        return CustomerUpdateDelivery.IsFailure(result.DeliveryStatus)
            ? Failed(update, sentByUserId, result.DeliveryStatus)
            : Delivered(update, sentByUserId, result.DeliveryStatus);
    }

    private async Task<CustomerUpdateDraft> SendWhatsAppAsync(CustomerUpdateDraft update, string sentByUserId, string? recipient, CancellationToken cancellationToken)
    {
        var waId = new string((recipient ?? string.Empty).Where(char.IsDigit).ToArray());
        if (waId.Length == 0 || whatsAppSenderResolver is null || whatsAppMessageRepository is null || consentRepository is null)
        {
            return Failed(update, sentByUserId, "ConfigurationRequired");
        }

        var sender = await whatsAppSenderResolver.ResolveAsync(update.Scope, cancellationToken);
        if (sender is null || !sender.Provider.IsConfigured)
        {
            return Failed(update, sentByUserId, "ConfigurationRequired");
        }

        // WhatsApp needs the customer's recorded agreement; the recency of an old chat is not agreement.
        var consent = await consentRepository.GetCurrentAsync(update.Scope, waId, ConsentPurposes.WhatsApp, cancellationToken);
        if (!ConsentEnforcementGuard.IsPermitted(consent))
        {
            return Failed(update, sentByUserId, "ConsentRequired");
        }

        var threadId = WhatsAppMessagingService.BuildThreadId(sender.BusinessPhoneNumberId, waId);
        var thread = await whatsAppMessageRepository.ListThreadAsync(update.Scope, threadId, cancellationToken);
        var lastInbound = thread.Where(message => message.Direction == WhatsAppMessageDirection.Inbound).Select(message => (DateTimeOffset?)message.CreatedAt).Max();
        var insideWindow = lastInbound is { } at && at >= DateTimeOffset.UtcNow - ServiceWindow;

        WhatsAppTemplateRef? template = null;
        var parameters = new Dictionary<string, string> { ["claim_reference"] = ClaimReference.For(update.CaseId) };
        if (!insideWindow)
        {
            // Outside the 24 hour window only an approved template may be sent, and it must be the one
            // the workspace set for this status. Without one the message waits for a person.
            if (settingsRepository is null
                || !ClaimStatusNotificationPolicy.TryStatusFromSourceKey(update.SourceTemplateKey, out var status))
            {
                return Failed(update, sentByUserId, "TemplateRequired");
            }

            var settings = await settingsRepository.GetAsync(update.Scope, cancellationToken);
            if (settings.WhatsAppStatusTemplates is null || !settings.WhatsAppStatusTemplates.TryGetValue(status.ToString(), out template))
            {
                return Failed(update, sentByUserId, "TemplateRequired");
            }

            parameters["status"] = ClaimantStatusWords.For(status, settings.ClaimantStatusWording).Headline;
        }

        var idempotencyKey = update.UpdateId.ToString("N");
        var result = await sender.Provider.SendAsync(new WhatsAppProviderSendRequest(
            sender.BusinessPhoneNumberId, waId, update.MessageBody,
            template?.Name, template?.Language ?? "en", ReplyToProviderMessageId: null, idempotencyKey,
            template is null ? null : parameters), cancellationToken);
        if (!result.Accepted)
        {
            return Failed(update, sentByUserId, "Failed");
        }

        var now = DateTimeOffset.UtcNow;
        await whatsAppMessageRepository.SaveAsync(new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = update.Scope,
            CaseId = update.CaseId,
            ThreadId = threadId,
            BusinessPhoneNumberId = sender.BusinessPhoneNumberId,
            ContactWaId = waId,
            Direction = WhatsAppMessageDirection.Outbound,
            ContentType = template is null ? WhatsAppContentType.Text : WhatsAppContentType.Template,
            // A message that carries a claim link is kept as a plain sentence: the link opens the claim.
            CustomerSafeBody = update.SourceTemplateKey == ClaimLinkPolicy.SourceKey ? ClaimLinkPolicy.RedactedBody : update.MessageBody,
            ProviderMessageId = result.ProviderMessageId,
            TemplateName = template?.Name,
            Language = template?.Language ?? "en",
            Purpose = "customer_update",
            ConsentBasis = "recorded_consent",
            ApprovedByUserId = update.ApprovedByUserId ?? sentByUserId,
            ApprovedAt = update.ApprovedAt ?? now,
            IdempotencyKey = idempotencyKey,
            DeliveryState = WhatsAppDeliveryState.ProviderAccepted,
            ProviderAcceptedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = update.CorrelationId,
        }, cancellationToken);
        return Delivered(update, sentByUserId, "Sent");
    }

    private static CustomerUpdateDraft Failed(CustomerUpdateDraft update, string sentByUserId, string deliveryStatus) =>
        update with { DeliveryStatus = deliveryStatus, SentByUserId = sentByUserId, UpdatedAt = DateTimeOffset.UtcNow };

    private static CustomerUpdateDraft Delivered(CustomerUpdateDraft update, string sentByUserId, string deliveryStatus) =>
        update with
        {
            Status = CustomerUpdateStatus.Sent,
            DeliveryStatus = deliveryStatus,
            SentByUserId = sentByUserId,
            SentAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static CommunicationSendRequest ToRequest(CustomerUpdateDraft update, string sentByUserId, string? recipient) =>
        new(
            update.Scope,
            update.CaseId,
            update.Channel,
            Recipient: recipient ?? string.Empty,
            update.MessageBody,
            sentByUserId,
            Approved: update.Status == CustomerUpdateStatus.Approved,
            LowRisk: !update.RequiresApproval,
            update.CorrelationId);
}
