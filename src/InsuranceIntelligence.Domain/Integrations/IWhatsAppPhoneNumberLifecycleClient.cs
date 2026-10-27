namespace InsuranceIntelligence.Domain.Integrations;

public enum WhatsAppPhoneNumberFailure
{
    None,
    WrongCode,
    CodeExpired,
    NumberInUse,
    RateLimited,
    DisplayNameRejected,
    NotConfigured,
    Other
}

public sealed record WhatsAppPhoneNumberResult(bool Success, string? SafeMessage = null, string? ProviderCode = null,
    WhatsAppPhoneNumberFailure Failure = WhatsAppPhoneNumberFailure.None);

public interface IWhatsAppPhoneNumberLifecycleClient
{
    Task<WhatsAppPhoneNumberResult> RequestCodeAsync(string phoneNumberId, string method, string language, string accessToken, CancellationToken cancellationToken = default);
    Task<WhatsAppPhoneNumberResult> VerifyCodeAsync(string phoneNumberId, string code, string accessToken, CancellationToken cancellationToken = default);
    Task<WhatsAppPhoneNumberResult> RegisterAsync(string phoneNumberId, string pin, string accessToken, CancellationToken cancellationToken = default);
    Task<WhatsAppPhoneNumberResult> SubscribeAppAsync(string wabaId, string accessToken, CancellationToken cancellationToken = default);
}
