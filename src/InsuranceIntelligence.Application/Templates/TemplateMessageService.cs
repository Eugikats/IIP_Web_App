using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Templates;

/// <summary>One approved-template message to send to someone who may not have written in the last 24 hours.</summary>
public sealed record TemplateSendRequest(
    string TemplateName,
    string Language,
    string RecipientWaId,
    IReadOnlyList<string> Values,
    string Purpose,
    string ConsentBasis,
    string IdempotencyKey,
    string ActorId,
    Guid? CaseId = null);

/// <summary>What happened to a template send. A blocked send says why in words a person can act on, and nothing was sent.</summary>
public sealed record TemplateSendResult(bool Sent, string State, string? Reason, Guid? MessageId, string? Text)
{
    public static TemplateSendResult Blocked(string reason) => new(false, "Blocked", reason, null, null);
}

/// <summary>
/// Sends an approved template. Unlike a reply inside the 24-hour window, a template may be the first thing a person
/// hears from the business, so the proof that they may be messaged is the consent the caller has checked and names, not
/// an earlier message from them. Everything else still holds: the template must be approved by WhatsApp, a recorded
/// withdrawal of consent stops it, marketing goes out only in sending hours, the words are filled with no gaps, a
/// retry never sends twice, and a workspace without a connected number sends nothing and says so.
/// </summary>
public sealed class TemplateMessageService(
    TemplateCatalogService catalogue,
    IWhatsAppChannelConfigurationRepository channelConfiguration,
    IWhatsAppMessageRepository messages,
    IWhatsAppOutboundProvider outbound,
    IConsentRepository? consentRepository = null)
{
    public async Task<TemplateSendResult> SendAsync(TenantScope scope, TemplateSendRequest request, CancellationToken cancellationToken = default)
    {
        var waId = Digits(request.RecipientWaId);
        if (waId.Length < 9)
        {
            return TemplateSendResult.Blocked("There is no usable phone number on record.");
        }

        var template = await catalogue.FindSendableAsync(scope, request.TemplateName, request.Language, cancellationToken);
        if (template is null)
        {
            var known = await catalogue.GetAsync(scope, request.TemplateName, request.Language, cancellationToken)
                ?? await catalogue.GetAsync(scope, request.TemplateName, DefaultTemplates.Language, cancellationToken);
            return TemplateSendResult.Blocked(known is null
                ? $"There is no template called {request.TemplateName}."
                : $"The template {known.Name} has not been approved by WhatsApp yet (it is {Describe(known.Status)}).");
        }

        if (consentRepository is not null)
        {
            var current = await consentRepository.GetCurrentAsync(scope, waId, ConsentPurposes.WhatsApp, cancellationToken);
            if (current is not null && !ConsentEnforcementGuard.IsPermitted(current))
            {
                return TemplateSendResult.Blocked("This person withdrew consent to be messaged on WhatsApp.");
            }
        }

        if (template.Category == TemplateCategory.Marketing && !OutreachService.WithinSendingHours(DateTimeOffset.UtcNow))
        {
            return TemplateSendResult.Blocked("Outside sending hours: marketing messages go out between 07:00 and 20:00 East Africa time.");
        }

        string text;
        try
        {
            text = template.Render(request.Values);
        }
        catch (TemplateRenderException failure)
        {
            return TemplateSendResult.Blocked(failure.Message);
        }

        var existing = await messages.FindByIdempotencyKeyAsync(scope, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new TemplateSendResult(
                existing.DeliveryState != WhatsAppDeliveryState.Failed, existing.DeliveryState.ToString(), existing.SafeError, existing.MessageId, existing.CustomerSafeBody);
        }

        var configuration = await channelConfiguration.GetAsync(scope, cancellationToken);
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.PhoneNumberId))
        {
            return TemplateSendResult.Blocked("WhatsApp is not connected for this workspace.");
        }

        if (!outbound.IsConfigured)
        {
            return TemplateSendResult.Blocked("WhatsApp outbound is not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var pending = new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = scope,
            CaseId = request.CaseId,
            ThreadId = WhatsAppMessagingService.BuildThreadId(configuration.PhoneNumberId, waId),
            BusinessPhoneNumberId = configuration.PhoneNumberId,
            ContactWaId = waId,
            Direction = WhatsAppMessageDirection.Outbound,
            ContentType = WhatsAppContentType.Template,
            CustomerSafeBody = text,
            TemplateName = template.Name,
            Language = template.Language,
            Purpose = request.Purpose.Trim(),
            ConsentBasis = request.ConsentBasis.Trim(),
            ApprovedByUserId = request.ActorId,
            ApprovedAt = now,
            IdempotencyKey = request.IdempotencyKey,
            DeliveryState = WhatsAppDeliveryState.Queued,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N"),
        };
        await messages.SaveAsync(pending, cancellationToken);

        WhatsAppMessage result;
        try
        {
            // Positional placeholders: the keys are the numbers, in order.
            var parameters = request.Values.Take(template.PlaceholderCount)
                .Select((value, index) => (Key: (index + 1).ToString(), Value: value))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var provider = await outbound.SendAsync(
                new WhatsAppProviderSendRequest(
                    pending.BusinessPhoneNumberId, waId, text, template.Name, template.Language, null, request.IdempotencyKey, parameters),
                cancellationToken);
            result = pending with
            {
                ProviderMessageId = provider.ProviderMessageId,
                DeliveryState = provider.Accepted ? WhatsAppDeliveryState.ProviderAccepted : WhatsAppDeliveryState.Failed,
                ProviderAcceptedAt = provider.Accepted ? DateTimeOffset.UtcNow : null,
                FailedAt = provider.Accepted ? null : DateTimeOffset.UtcNow,
                ProviderErrorCode = provider.ProviderErrorCode,
                SafeError = provider.SafeError,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception failure) when (failure is OperationCanceledException or HttpRequestException && !cancellationToken.IsCancellationRequested)
        {
            // Not knowing is not failing: the message may have left. Hold it for reconciliation instead of risking a second send.
            result = pending with
            {
                DeliveryState = WhatsAppDeliveryState.Unknown,
                SafeError = "The provider did not confirm. Check before trying again.",
                UpdatedAt = DateTimeOffset.UtcNow,
            };
        }

        await messages.SaveAsync(result, cancellationToken);
        return result.DeliveryState == WhatsAppDeliveryState.Failed
            ? TemplateSendResult.Blocked(result.SafeError ?? "WhatsApp did not accept the message.")
            : new TemplateSendResult(true, result.DeliveryState.ToString(), null, result.MessageId, text);
    }

    private static string Describe(TemplateStatus status) => status switch
    {
        TemplateStatus.Draft => "still a draft",
        TemplateStatus.AwaitingApproval => "waiting for WhatsApp's decision",
        TemplateStatus.Rejected => "rejected",
        TemplateStatus.Paused => "paused",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());
}
