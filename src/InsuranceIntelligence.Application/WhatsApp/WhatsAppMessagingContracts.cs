using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.WhatsApp;

public sealed record SendWhatsAppMessageCommand(
    Guid CaseId,
    string ThreadId,
    string RecipientWaId,
    string CustomerSafeBody,
    string Purpose,
    string ConsentBasis,
    string Language,
    string? TemplateName,
    bool HumanApproved,
    string IdempotencyKey,
    string? ReplyToProviderMessageId,
    IReadOnlyDictionary<string, string>? TemplateParameters = null);

public sealed record WhatsAppMessageDto(
    Guid MessageId,
    Guid? CaseId,
    string ThreadId,
    string BusinessPhoneNumberId,
    string ContactWaId,
    WhatsAppMessageDirection Direction,
    WhatsAppContentType ContentType,
    string CustomerSafeBody,
    string? ProviderMessageId,
    string? TemplateName,
    string? Language,
    WhatsAppDeliveryState DeliveryState,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? SafeError);

public sealed class WhatsAppPolicyException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public interface IWhatsAppMessagingService
{
    Task<IReadOnlyList<WhatsAppMessageDto>> GetThreadAsync(string threadId, CancellationToken cancellationToken = default);
    Task<WhatsAppMessageDto> SendAsync(SendWhatsAppMessageCommand command, CancellationToken cancellationToken = default);
    Task<int> IngestInboundAsync(TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, CancellationToken cancellationToken = default);
    Task<int> ApplyDeliveryEventsAsync(TenantScope scope, IReadOnlyList<WhatsAppDeliveryEvent> events, CancellationToken cancellationToken = default);
}
