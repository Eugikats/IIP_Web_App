using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// Provider-neutral inbound WhatsApp media message (image/document). Meta/Twilio/Infobip
/// payload shapes stay adapter-internal.
/// </summary>
public sealed record WhatsAppInboundMediaMessage(
    string ProviderMessageId,
    string BusinessPhoneNumberId,
    string SenderWaId,
    string MediaId,
    string MediaType,
    string? MimeType,
    string? FileName,
    string? Caption,
    DateTimeOffset ReceivedAt);

public sealed record WhatsAppMediaContent(
    string MediaId,
    string ContentType,
    string FileName,
    byte[] ContentBytes);

public sealed record WhatsAppToCaseMatchResult(
    Guid? CaseId,
    string MatchReason,
    bool RequiresManualReview,
    IdentifiedSender? IdentifiedSender = null);

public sealed record WhatsAppIngestionResult(
    string Status,
    int MediaMessagesSeen,
    int DocumentsStored,
    int ManualReviewRequired,
    string? SafeError);

public enum WhatsAppMessageDirection { Inbound, Outbound }
public enum WhatsAppContentType { Text, Image, Document, Template, Unsupported, Audio }
public enum WhatsAppDeliveryState { Received, Queued, ProviderAccepted, Delivered, Read, Failed, Unknown }

public sealed record WhatsAppInboundMessage(
    string ProviderMessageId,
    string BusinessPhoneNumberId,
    string SenderWaId,
    WhatsAppContentType ContentType,
    string CustomerSafeBody,
    string? ReplyToProviderMessageId,
    DateTimeOffset ReceivedAt)
{
    /// <summary>The name the sender set on their own WhatsApp profile. Chosen by them, unverified:
    /// good for a greeting, never proof of who they are.</summary>
    public string? ProfileName { get; init; }

    /// <summary>For a voice note, the Meta media id to download it with.</summary>
    public string? MediaId { get; init; }
}

public sealed record WhatsAppDeliveryEvent(
    string ProviderMessageId,
    string BusinessPhoneNumberId,
    WhatsAppDeliveryState DeliveryState,
    DateTimeOffset OccurredAt,
    string? ProviderErrorCode,
    string? SafeError);

public sealed record WhatsAppMessage
{
    public required Guid MessageId { get; init; }
    public required TenantScope Scope { get; init; }
    public Guid? CaseId { get; init; }
    public required string ThreadId { get; init; }
    public required string BusinessPhoneNumberId { get; init; }
    public required string ContactWaId { get; init; }
    public required WhatsAppMessageDirection Direction { get; init; }
    public required WhatsAppContentType ContentType { get; init; }
    public required string CustomerSafeBody { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ReplyToProviderMessageId { get; init; }

    /// <summary>For an inbound voice note, the Meta media id to fetch it with. Kept so the assistant's
    /// background job can be rebuilt from the stored message alone (build-loop item 27).</summary>
    public string? MediaId { get; init; }

    /// <summary>The name the sender set on their own WhatsApp profile, kept for the same reason.</summary>
    public string? ProfileName { get; init; }
    public string? TemplateName { get; init; }
    public string? Language { get; init; }
    public string? Purpose { get; init; }
    public string? ConsentBasis { get; init; }
    public string? ApprovedByUserId { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public string? IdempotencyKey { get; init; }
    public required WhatsAppDeliveryState DeliveryState { get; init; }
    public DateTimeOffset? ProviderAcceptedAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
    public DateTimeOffset? FailedAt { get; init; }
    public string? ProviderErrorCode { get; init; }
    public string? SafeError { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CorrelationId { get; init; }

    /// <summary>For an inbound message: when the assistant finished with it (answered, or handed to a
    /// person). A message that stays empty here long after it arrived was lost in a restart.</summary>
    public DateTimeOffset? AssistantHandledAt { get; init; }
}

public sealed record WhatsAppProviderSendRequest(
    string BusinessPhoneNumberId,
    string RecipientWaId,
    string CustomerSafeBody,
    string? TemplateName,
    string Language,
    string? ReplyToProviderMessageId,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string>? TemplateParameters = null,
    string? TemplateButtonPayload = null);

public sealed record WhatsAppProviderSendResult(
    bool Accepted,
    string? ProviderMessageId,
    string? ProviderErrorCode,
    string? SafeError);
