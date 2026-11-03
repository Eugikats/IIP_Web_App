namespace InsuranceIntelligence.Application.Channels;

public interface IChannelQueryService
{
    Task<IReadOnlyList<ChannelConversationRowDto>> GetOverviewAsync(CancellationToken cancellationToken = default);
}

public interface IChannelReadRepository
{
    Task<IReadOnlyList<ChannelConversationReadModel>> GetOverviewAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed record ChannelConversationReadModel(
    string Id,
    string CustomerName,
    string Channel,
    string DetectedIntent,
    string SuggestedRouting,
    decimal AiConfidence,
    string Status,
    string? AssignedTo,
    string LastMessageSnippet,
    DateTimeOffset LastActivityAt,
    string? CustomerContact = null,
    string? CustomerLanguage = null,
    string? LanguageNote = null)
{
    /// <summary>Who is answering a WhatsApp conversation: "Kato" or "Person".</summary>
    public string ControlMode { get; init; } = "Kato";
}

public sealed record ChannelConversationRowDto(
    string Id,
    string CustomerName,
    string Channel,
    string DetectedIntent,
    string SuggestedRouting,
    decimal AiConfidence,
    string Status,
    string? AssignedTo,
    string LastMessage,
    string LastActivityAt,
    string? CustomerContact = null,
    string? CustomerLanguage = null,
    string? LanguageNote = null)
{
    public string ControlMode { get; init; } = "Kato";
}

public static class ChannelValues
{
    public const string Unknown = "unknown";
    public const string WhatsApp = "whatsapp";
    public const string Instagram = "instagram";
    public const string Messenger = "messenger";
    public const string TikTok = "tiktok";
    public const string X = "x";
    public const string Telegram = "telegram";
    public const string WebChat = "webchat";
    public const string Sms = "sms";
    public const string Email = "email";
    public const string CallCenter = "call_center";
    public const string Broker = "broker";
    public const string WebsiteForm = "website_form";
}

public static class ChannelDetectedIntentValues
{
    public const string ClaimFnol = "claim_fnol";
    public const string QuoteRequest = "quote_request";
    public const string Renewal = "renewal";
    public const string Complaint = "complaint";
    public const string PolicyInquiry = "policy_inquiry";
    public const string DocumentUpload = "document_upload";
    public const string PaymentInquiry = "payment_inquiry";
    public const string GeneralService = "general_service";

    // Added so the customer assistant's own classification survives the trip to the Channels
    // inbox. CustomerIntent has six values; squeezing ExistingClaimStatus into policy_inquiry or
    // HumanRequested into general_service would have shown an officer the wrong reason for the
    // conversation, which is worse than a longer vocabulary.
    public const string ClaimStatus = "claim_status";
    public const string HumanRequested = "human_requested";
}
