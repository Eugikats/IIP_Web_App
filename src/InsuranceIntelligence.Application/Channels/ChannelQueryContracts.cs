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
    string? CustomerContact = null);

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
    string? CustomerContact = null);

public static class ChannelValues
{
    public const string WhatsApp = "whatsapp";
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
}
