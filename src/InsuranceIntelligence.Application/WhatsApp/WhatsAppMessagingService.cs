using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using System.Security.Cryptography;
using System.Text;

namespace InsuranceIntelligence.Application.WhatsApp;

public sealed class WhatsAppMessagingService(
    ICaseRepository caseRepository,
    IWhatsAppMessageRepository repository,
    IWhatsAppOutboundProvider outboundProvider,
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAuditWriter auditWriter,
    IConsentRepository? consentRepository = null,
    // The webhook path has no signed-in user, so its audit events are written with the scope the
    // verified channel configuration resolved, straight to the repository, the same way
    // CustomerAssistantService writes its own. AuditWriter (above) rightly refuses to write
    // without a current tenant context; it stays in use for every staff-initiated action here.
    IAuditRepository? integrationAuditRepository = null) : IWhatsAppMessagingService
{
    private static readonly string[] RestrictedTerms =
        ["raw prompt", "system message", "internal note", "fraud flag", "reserve", "hidden risk", "secret"];

    public async Task<IReadOnlyList<WhatsAppMessageDto>> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewChannels, cancellationToken);
        if (string.IsNullOrWhiteSpace(threadId)) throw new ArgumentException("Thread id is required.", nameof(threadId));
        var context = tenantContextAccessor.Current;
        var messages = await repository.ListThreadAsync(context.Scope, threadId.Trim(), cancellationToken);
        await AuditAsync(context, null, "whatsapp.thread.viewed", $"messages:{messages.Count}", cancellationToken);
        return messages.Select(ToDto).ToArray();
    }

    public async Task<WhatsAppMessageDto> SendAsync(
        SendWhatsAppMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        await permissionService.RequirePermissionAsync(Permission.SendCustomerUpdate, cancellationToken);
        Validate(command);
        var context = tenantContextAccessor.Current;
        var existing = await repository.FindByIdempotencyKeyAsync(context.Scope, command.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            if (existing.CaseId != command.CaseId || existing.ThreadId != command.ThreadId || existing.CustomerSafeBody != command.CustomerSafeBody)
                throw new WhatsAppPolicyException("IdempotencyConflict", "That send key is already bound to a different WhatsApp message.");
            return ToDto(existing);
        }

        // A claim is optional: staff also reply to prospects and to people who have not reported
        // anything. When a claim is named it must still be this workspace's.
        if (command.CaseId is { } caseId)
        {
            var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
            if (claimCase is null || !claimCase.Scope.Matches(context.Scope))
                throw new WhatsAppPolicyException("CaseNotFound", "The case was not found in this tenant workspace.");
        }

        var thread = await repository.ListThreadAsync(context.Scope, command.ThreadId.Trim(), cancellationToken);
        if (thread.Any(message => message.Direction == WhatsAppMessageDirection.Outbound
            && message.DeliveryState is WhatsAppDeliveryState.Queued or WhatsAppDeliveryState.Unknown))
            throw new WhatsAppPolicyException("ReconciliationRequired", "A previous WhatsApp attempt is unresolved. Reconcile it before trying again.");
        // Verification is "this contact actually sent an inbound message on this exact thread",
        // not "that inbound message already carries this case id": nothing in the inbound pipeline
        // (CustomerAssistantService) ever stamps a CaseId onto the customer's own message, only
        // onto the assistant's outbound reply once a case is opened, so every inbound row's CaseId
        // is null in practice. Requiring an inbound/command CaseId match here made every reply
        // attempt fail with RecipientNotVerified, on every thread, case-linked or not. ThreadId is
        // already scoped to one exact contact (WhatsAppMessagingService.BuildThreadId), so the
        // ContactWaId match below is what actually proves this is a real, consenting participant.
        var verifiedInbound = thread
            .Where(message => message.Direction == WhatsAppMessageDirection.Inbound
                && string.Equals(message.ContactWaId, NormalizeWaId(command.RecipientWaId), StringComparison.Ordinal))
            .OrderByDescending(message => message.CreatedAt)
            .FirstOrDefault();
        if (verifiedInbound is null)
            throw new WhatsAppPolicyException("RecipientNotVerified", "The recipient has not sent a message on this WhatsApp thread.");

        // Outcome M / AGENTS.md non-negotiable 5: a staff-typed ConsentBasis string (below) is a
        // claim, not a check. This is the real one, against the same ConsentRecord table every
        // inbound WhatsApp message already writes to (CustomerAssistantService), so it is normally
        // satisfied automatically by the verifiedInbound check just above -- it only actually blocks
        // a send after a recorded withdrawal, which recency of an old inbound message cannot see.
        if (consentRepository is not null)
        {
            var consent = await consentRepository.GetCurrentAsync(context.Scope, NormalizeWaId(command.RecipientWaId), ConsentPurposes.WhatsApp, cancellationToken);
            if (!ConsentEnforcementGuard.IsPermitted(consent))
                throw new WhatsAppPolicyException("ConsentRequired", "No recorded, unwithdrawn consent for WhatsApp messages to this recipient.");
        }

        var outsideWindow = verifiedInbound.CreatedAt < DateTimeOffset.UtcNow.AddHours(-24);
        if (outsideWindow && string.IsNullOrWhiteSpace(command.TemplateName))
            throw new WhatsAppPolicyException("TemplateRequired", "The customer-service window has ended. Select an approved WhatsApp template.");
        if (!outboundProvider.IsConfigured)
            throw new WhatsAppPolicyException("ConfigurationRequired", "WhatsApp outbound is not configured.");

        var now = DateTimeOffset.UtcNow;
        var pending = new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(), Scope = context.Scope, CaseId = command.CaseId,
            ThreadId = command.ThreadId.Trim(), BusinessPhoneNumberId = verifiedInbound.BusinessPhoneNumberId,
            ContactWaId = NormalizeWaId(command.RecipientWaId), Direction = WhatsAppMessageDirection.Outbound,
            ContentType = outsideWindow ? WhatsAppContentType.Template : WhatsAppContentType.Text,
            CustomerSafeBody = command.CustomerSafeBody.Trim(), ReplyToProviderMessageId = Normalize(command.ReplyToProviderMessageId),
            TemplateName = Normalize(command.TemplateName), Language = command.Language.Trim(), Purpose = command.Purpose.Trim(),
            ConsentBasis = command.ConsentBasis.Trim(), ApprovedByUserId = context.UserId, ApprovedAt = now,
            IdempotencyKey = command.IdempotencyKey.Trim(), DeliveryState = WhatsAppDeliveryState.Queued,
            CreatedAt = now, UpdatedAt = now, CorrelationId = Guid.NewGuid().ToString("N")
        };
        await repository.SaveAsync(pending, cancellationToken);

        WhatsAppMessage result;
        try
        {
            var providerResult = await outboundProvider.SendAsync(new WhatsAppProviderSendRequest(
                pending.BusinessPhoneNumberId, pending.ContactWaId, pending.CustomerSafeBody, pending.TemplateName,
                pending.Language!, pending.ReplyToProviderMessageId, pending.IdempotencyKey!, command.TemplateParameters), cancellationToken);
            result = pending with
            {
                ProviderMessageId = Normalize(providerResult.ProviderMessageId),
                DeliveryState = providerResult.Accepted ? WhatsAppDeliveryState.ProviderAccepted : WhatsAppDeliveryState.Failed,
                ProviderAcceptedAt = providerResult.Accepted ? DateTimeOffset.UtcNow : null,
                FailedAt = providerResult.Accepted ? null : DateTimeOffset.UtcNow,
                ProviderErrorCode = Normalize(providerResult.ProviderErrorCode),
                SafeError = Normalize(providerResult.SafeError),
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = pending with { DeliveryState = WhatsAppDeliveryState.Unknown, SafeError = "Provider acknowledgement timed out. Reconcile before retrying.", UpdatedAt = DateTimeOffset.UtcNow };
        }
        catch (HttpRequestException)
        {
            result = pending with { DeliveryState = WhatsAppDeliveryState.Unknown, SafeError = "Provider acknowledgement was not received. Reconcile before retrying.", UpdatedAt = DateTimeOffset.UtcNow };
        }
        await repository.SaveAsync(result, cancellationToken);
        await AuditAsync(context, command.CaseId, "whatsapp.outbound.attempted", $"state:{result.DeliveryState};message:{result.MessageId}", cancellationToken);
        return ToDto(result);
    }

    public async Task<IReadOnlyList<WhatsAppInboundMessage>> IngestInboundAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppInboundMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var newlySaved = new List<WhatsAppInboundMessage>();
        foreach (var inbound in messages)
        {
            if (await repository.FindByProviderMessageIdAsync(scope, inbound.ProviderMessageId, cancellationToken) is not null) continue;
            var caseId = await ResolveCaseIdAsync(scope, inbound.CustomerSafeBody, cancellationToken);
            var waId = NormalizeWaId(inbound.SenderWaId);
            var message = new WhatsAppMessage
            {
                MessageId = Guid.NewGuid(), Scope = scope, CaseId = caseId,
                ThreadId = BuildThreadId(inbound.BusinessPhoneNumberId, waId),
                BusinessPhoneNumberId = inbound.BusinessPhoneNumberId, ContactWaId = waId,
                Direction = WhatsAppMessageDirection.Inbound, ContentType = inbound.ContentType,
                CustomerSafeBody = inbound.ContentType == WhatsAppContentType.Text
                    && inbound.CustomerSafeBody.Trim().Length == 6
                    && inbound.CustomerSafeBody.Trim().All(char.IsAsciiDigit)
                    ? "[verification code redacted]" : inbound.CustomerSafeBody,
                ProviderMessageId = inbound.ProviderMessageId,
                ReplyToProviderMessageId = inbound.ReplyToProviderMessageId,
                DeliveryState = WhatsAppDeliveryState.Received, CreatedAt = inbound.ReceivedAt,
                UpdatedAt = inbound.ReceivedAt, CorrelationId = Guid.NewGuid().ToString("N")
            };
            await repository.SaveAsync(message, cancellationToken);
            newlySaved.Add(inbound);
        }
        if (newlySaved.Count > 0)
            await WriteIntegrationAuditAsync(scope, "whatsapp.inbound.received", $"messages:{newlySaved.Count}", cancellationToken);
        return newlySaved;
    }

    public async Task<int> ApplyDeliveryEventsAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppDeliveryEvent> events,
        CancellationToken cancellationToken = default)
    {
        var changed = 0;
        foreach (var delivery in events)
        {
            var message = await repository.FindByProviderMessageIdAsync(scope, delivery.ProviderMessageId, cancellationToken);
            if (message is null || message.Direction != WhatsAppMessageDirection.Outbound
                || !string.Equals(message.BusinessPhoneNumberId, delivery.BusinessPhoneNumberId, StringComparison.Ordinal)) continue;
            var updated = Apply(message, delivery);
            if (updated == message) continue;
            await repository.SaveAsync(updated, cancellationToken);
            changed++;
        }
        if (changed > 0)
            await WriteIntegrationAuditAsync(scope, "whatsapp.delivery.reconciled", $"statuses:{changed}", cancellationToken);
        return changed;
    }

    private static WhatsAppMessage Apply(WhatsAppMessage message, WhatsAppDeliveryEvent delivery)
    {
        var deliveredAt = message.DeliveredAt;
        var readAt = message.ReadAt;
        var failedAt = message.FailedAt;
        if (delivery.DeliveryState is WhatsAppDeliveryState.Delivered or WhatsAppDeliveryState.Read)
            deliveredAt ??= delivery.OccurredAt;
        if (delivery.DeliveryState == WhatsAppDeliveryState.Read) readAt ??= delivery.OccurredAt;
        if (delivery.DeliveryState == WhatsAppDeliveryState.Failed) failedAt ??= delivery.OccurredAt;

        var currentRank = DeliveryRank(message.DeliveryState);
        var incomingRank = DeliveryRank(delivery.DeliveryState);
        var state = incomingRank >= currentRank ? delivery.DeliveryState : message.DeliveryState;
        if (state == message.DeliveryState && deliveredAt == message.DeliveredAt && readAt == message.ReadAt && failedAt == message.FailedAt)
            return message;
        return message with
        {
            DeliveryState = state, DeliveredAt = deliveredAt, ReadAt = readAt, FailedAt = failedAt,
            ProviderErrorCode = delivery.DeliveryState == WhatsAppDeliveryState.Failed ? Normalize(delivery.ProviderErrorCode) : message.ProviderErrorCode,
            SafeError = delivery.DeliveryState == WhatsAppDeliveryState.Failed ? Normalize(delivery.SafeError) : message.SafeError,
            UpdatedAt = delivery.OccurredAt
        };
    }

    private async Task<Guid?> ResolveCaseIdAsync(TenantScope scope, string body, CancellationToken cancellationToken)
    {
        foreach (var token in body.Split([' ', '\r', '\n', '\t', ',', '.', ':', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Guid.TryParse(token, out var candidate)) continue;
            var claimCase = await caseRepository.GetAsync(candidate, cancellationToken);
            if (claimCase is not null && claimCase.Scope.Matches(scope)) return candidate;
        }
        return null;
    }

    private static void Validate(SendWhatsAppMessageCommand command)
    {
        if (!command.HumanApproved) throw new WhatsAppPolicyException("ApprovalRequired", "Review and approve the exact customer message before sending.");
        if (string.IsNullOrWhiteSpace(command.ThreadId)) throw new WhatsAppPolicyException("ThreadRequired", "A case-linked WhatsApp thread is required.");
        if (string.IsNullOrWhiteSpace(command.RecipientWaId)) throw new WhatsAppPolicyException("RecipientRequired", "A verified WhatsApp recipient is required.");
        if (string.IsNullOrWhiteSpace(command.CustomerSafeBody)) throw new WhatsAppPolicyException("MessageRequired", "Enter the customer message to send.");
        if (command.CustomerSafeBody.Length > 4096) throw new WhatsAppPolicyException("MessageTooLong", "WhatsApp messages must be 4,096 characters or fewer.");
        if (RestrictedTerms.Any(term => command.CustomerSafeBody.Contains(term, StringComparison.OrdinalIgnoreCase)))
            throw new WhatsAppPolicyException("RestrictedContent", "Remove internal or restricted content before sending.");
        if (string.IsNullOrWhiteSpace(command.Purpose)) throw new WhatsAppPolicyException("PurposeRequired", "Record the communication purpose before sending.");
        if (string.IsNullOrWhiteSpace(command.ConsentBasis)) throw new WhatsAppPolicyException("ConsentBasisRequired", "Record the approved consent or legal basis before sending.");
        if (string.IsNullOrWhiteSpace(command.Language)) throw new WhatsAppPolicyException("LanguageRequired", "Select the message language before sending.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128)
            throw new WhatsAppPolicyException("IdempotencyKeyRequired", "A valid idempotency key is required.");
    }

    private Task AuditAsync(TenantContext context, Guid? caseId, string action, string outcome, CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(new AuditEvent(
            Guid.NewGuid(), context.Scope, caseId, null, context.UserId, ActorType.User, action,
            DateTimeOffset.UtcNow, null, null, [], "WhatsApp", null, outcome, null,
            "Customer communication metadata recorded; message body omitted.", Guid.NewGuid().ToString("N")), cancellationToken);

    // Found live on 2026-09-25: routed through AuditWriter, this threw "Tenant context has not been
    // initialized" on every inbound message, after the message itself had been saved, so the
    // webhook answered 500 and the customer's message was only handled on Meta's retry, if at all.
    private Task WriteIntegrationAuditAsync(TenantScope scope, string action, string outcome, CancellationToken cancellationToken)
    {
        var auditEvent = new AuditEvent(
            Guid.NewGuid(), scope, null, null, "system:whatsapp-webhook", ActorType.Integration, action,
            DateTimeOffset.UtcNow, null, null, [], "WhatsApp", null, outcome, null,
            "Verified WhatsApp webhook metadata recorded; message body omitted.", Guid.NewGuid().ToString("N"));
        return integrationAuditRepository is not null
            ? integrationAuditRepository.WriteAsync(auditEvent, cancellationToken)
            : auditWriter.WriteAsync(auditEvent, cancellationToken);
    }

    public static string BuildThreadId(string businessPhoneNumberId, string contactWaId) =>
        $"wa:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{businessPhoneNumberId.Trim()}:{NormalizeWaId(contactWaId)}")))[..32].ToLowerInvariant()}";
    private static string NormalizeWaId(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int DeliveryRank(WhatsAppDeliveryState value) => value switch
    {
        WhatsAppDeliveryState.Queued => 0,
        WhatsAppDeliveryState.Unknown => 1,
        WhatsAppDeliveryState.ProviderAccepted => 2,
        WhatsAppDeliveryState.Failed => 3,
        WhatsAppDeliveryState.Delivered => 4,
        WhatsAppDeliveryState.Read => 5,
        _ => 0
    };
    private static WhatsAppMessageDto ToDto(WhatsAppMessage message) => new(
        message.MessageId, message.CaseId, message.ThreadId, message.BusinessPhoneNumberId,
        message.ContactWaId, message.Direction, message.ContentType, message.CustomerSafeBody,
        message.ProviderMessageId, message.TemplateName, message.Language, message.DeliveryState,
        message.CreatedAt, message.UpdatedAt, message.SafeError, message.ApprovedByUserId);
}
