using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Cases;

/// <summary>What actually happened when the platform tried to ask a customer for missing
/// documents (blueprint §14 Phase 5, WO 5.4), always truthful.</summary>
public enum MissingDocumentRequestOutcome
{
    /// <summary>Actually delivered to the sending provider.</summary>
    Sent,

    /// <summary>Nothing is missing, there is no request to send.</summary>
    SkippedNoMissingDocuments,

    /// <summary>The claim's channel has no usable contact on file (a WhatsApp id or a phone
    /// number/email address), or WhatsApp is the origin channel but the tenant's business number
    /// was not resolved by the caller.</summary>
    NoRecipientAvailable,

    /// <summary>A channel and recipient were resolved but the send did not go out, pending human
    /// approval, an unconfigured provider, or a provider-side failure. The reason is in the audit
    /// trail, not swallowed.</summary>
    NotSent,

    /// <summary>WhatsApp only (blueprint §14 Phase 6, WO 6.1): no active ConsentRecord exists for
    /// this customer and purpose, the original inbound conversation that would have captured one
    /// may be long over. Never sends without one.</summary>
    NoActiveConsent,
}

public interface IMissingDocumentRequestService
{
    /// <summary>Sends (or honestly declines to send) a request for the claim's still-missing
    /// required documents, over the channel the claim actually originated on (blueprint WO 5.4's
    /// explicit requirement, unlike WO 5.2's acknowledgement, which is channel-agnostic).
    /// whatsAppBusinessPhoneNumberId is the tenant's configured WhatsApp Cloud API phone number id
    /// (IWhatsAppRuntimeSettingsProvider.Current?.PhoneNumberId), resolved and passed in by the
    /// caller, which lives in the Api layer and can reach Infrastructure; this service cannot
    /// reference that provider directly for the same reason CustomerAssistantService takes
    /// autoReplyEnabled as a parameter instead of reading InfrastructureOptions itself.</summary>
    Task<MissingDocumentRequestOutcome> SendAsync(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        string correlationId,
        string? whatsAppBusinessPhoneNumberId,
        CancellationToken cancellationToken = default);
}

public sealed class MissingDocumentRequestService(
    IWhatsAppOutboundProvider whatsAppOutboundProvider,
    IWhatsAppMessageRepository whatsAppMessageRepository,
    ICommunicationSendingProvider communicationSendingProvider,
    ICommunicationRepository communicationRepository,
    IConsentRepository consentRepository,
    IAuditRepository auditRepository) : IMissingDocumentRequestService
{
    private const string SystemActorId = "system:missing-document-request";
    private const string WhatsAppMessagingPurpose = "whatsapp_messaging";

    public async Task<MissingDocumentRequestOutcome> SendAsync(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        string correlationId,
        string? whatsAppBusinessPhoneNumberId,
        CancellationToken cancellationToken = default)
    {
        if (checklist.MissingRequiredDocuments.Count == 0)
        {
            return MissingDocumentRequestOutcome.SkippedNoMissingDocuments;
        }

        var reference = claimCase.CaseId.ToString()[..8].ToUpperInvariant();
        var missingLabels = string.Join(", ", checklist.MissingRequiredDocuments.Select(missing => missing.Label));
        var messageBody = $"We're still waiting on some documents for your claim {reference}: {missingLabels}. Please send them at your earliest convenience so we can continue reviewing your claim.";

        return string.Equals(claimCase.Channel, "WhatsApp", StringComparison.OrdinalIgnoreCase)
            ? await SendOverWhatsAppAsync(claimCase, messageBody, whatsAppBusinessPhoneNumberId, correlationId, cancellationToken)
            : await SendOverCommunicationChannelAsync(claimCase, messageBody, correlationId, cancellationToken);
    }

    private async Task<MissingDocumentRequestOutcome> SendOverWhatsAppAsync(
        ClaimCase claimCase, string messageBody, string? businessPhoneNumberId, string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(businessPhoneNumberId) || string.IsNullOrWhiteSpace(claimCase.CustomerPhone))
        {
            await WriteAuditAsync(claimCase, "NoRecipientAvailable", correlationId, cancellationToken);
            return MissingDocumentRequestOutcome.NoRecipientAvailable;
        }

        if (!whatsAppOutboundProvider.IsConfigured)
        {
            await WriteAuditAsync(claimCase, "ConfigurationRequired", correlationId, cancellationToken);
            return MissingDocumentRequestOutcome.NotSent;
        }

        var waId = NormalizeWaId(claimCase.CustomerPhone);

        // WO 6.1 outbound enforcement, this send has no fresh inbound message to lean on (it is
        // triggered by workflow evaluation, not a live webhook), so the consent capture from the
        // original conversation is the only thing that can permit it.
        var consent = await consentRepository.GetCurrentAsync(claimCase.Scope, waId, WhatsAppMessagingPurpose, cancellationToken);
        if (!ConsentEnforcementGuard.IsPermitted(consent))
        {
            await WriteAuditAsync(claimCase, "NoActiveConsent", correlationId, cancellationToken);
            return MissingDocumentRequestOutcome.NoActiveConsent;
        }

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var result = await whatsAppOutboundProvider.SendAsync(new WhatsAppProviderSendRequest(
            businessPhoneNumberId, waId, messageBody, TemplateName: null, Language: "en",
            ReplyToProviderMessageId: null, IdempotencyKey: idempotencyKey), cancellationToken);

        if (!result.Accepted)
        {
            await WriteAuditAsync(claimCase, result.ProviderErrorCode ?? "ProviderError", correlationId, cancellationToken);
            return MissingDocumentRequestOutcome.NotSent;
        }

        await whatsAppMessageRepository.SaveAsync(new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            ThreadId = WhatsAppMessagingService.BuildThreadId(businessPhoneNumberId, waId),
            BusinessPhoneNumberId = businessPhoneNumberId,
            ContactWaId = waId,
            Direction = WhatsAppMessageDirection.Outbound,
            ContentType = WhatsAppContentType.Text,
            CustomerSafeBody = messageBody,
            ProviderMessageId = result.ProviderMessageId,
            Purpose = "missing_document_request",
            ConsentBasis = "customer_initiated_conversation",
            ApprovedByUserId = SystemActorId,
            ApprovedAt = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey,
            DeliveryState = WhatsAppDeliveryState.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);

        await WriteAuditAsync(claimCase, "Sent", correlationId, cancellationToken);
        return MissingDocumentRequestOutcome.Sent;
    }

    private async Task<MissingDocumentRequestOutcome> SendOverCommunicationChannelAsync(
        ClaimCase claimCase, string messageBody, string correlationId, CancellationToken cancellationToken)
    {
        var (channel, recipient) = ResolveRecipient(claimCase);
        if (channel is null || recipient is null)
        {
            await WriteAuditAsync(claimCase, "NoRecipientAvailable", correlationId, cancellationToken);
            return MissingDocumentRequestOutcome.NoRecipientAvailable;
        }

        var request = new CommunicationSendRequest(
            claimCase.Scope, claimCase.CaseId, channel.Value, recipient, messageBody,
            SystemActorId, Approved: false, LowRisk: true, correlationId);

        var result = channel.Value == CommunicationChannel.SMS
            ? await communicationSendingProvider.SendSmsAsync(request, cancellationToken)
            : await communicationSendingProvider.SendEmailAsync(request, cancellationToken);

        if (result.DeliveryStatus != "Sent")
        {
            await WriteAuditAsync(claimCase, result.DeliveryStatus, correlationId, cancellationToken);
            return MissingDocumentRequestOutcome.NotSent;
        }

        await communicationRepository.SaveAsync(new Communication
        {
            CommunicationId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            Channel = channel.Value,
            Direction = "Outbound",
            MessageBody = messageBody,
            TemplateId = "missing-document-request",
            SentBy = SystemActorId,
            DeliveryStatus = result.DeliveryStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            ProviderMessageId = string.IsNullOrWhiteSpace(result.ProviderMessageId) ? null : result.ProviderMessageId,
        }, cancellationToken);

        await WriteAuditAsync(claimCase, "Sent", correlationId, cancellationToken);
        return MissingDocumentRequestOutcome.Sent;
    }

    private static (CommunicationChannel? Channel, string? Recipient) ResolveRecipient(ClaimCase claimCase)
    {
        if (!string.IsNullOrWhiteSpace(claimCase.CustomerPhone))
        {
            return (CommunicationChannel.SMS, claimCase.CustomerPhone);
        }

        if (!string.IsNullOrWhiteSpace(claimCase.CustomerEmail))
        {
            return (CommunicationChannel.Email, claimCase.CustomerEmail);
        }

        return (null, null);
    }

    private static string NormalizeWaId(string value) => new(value.Where(char.IsDigit).ToArray());

    private Task WriteAuditAsync(ClaimCase claimCase, string outcome, string correlationId, CancellationToken cancellationToken) =>
        auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), claimCase.Scope, claimCase.CaseId, null, SystemActorId, ActorType.Integration,
                "missing_document_request", DateTimeOffset.UtcNow,
                null, null, [], "MissingDocumentRequestService", null,
                outcome, null,
                $"Missing-document request for case {claimCase.CaseId}: {outcome}.",
                correlationId),
            cancellationToken);
}
