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

public sealed record WhatsAppToCaseMatchResult(Guid? CaseId, string MatchReason, bool RequiresManualReview);

public sealed record WhatsAppIngestionResult(
    string Status,
    int MediaMessagesSeen,
    int DocumentsStored,
    int ManualReviewRequired,
    string? SafeError);
