namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// Confirms a WhatsApp Business phone number id + access token actually work against the real
/// Meta Graph API before IIP ever persists them — "guide me through" means the settings screen
/// tells the operator immediately whether what they typed is correct, not after the next inbound
/// webhook silently fails.
/// </summary>
public interface IWhatsAppGraphValidationClient
{
    Task<WhatsAppGraphValidationResult> ValidateAsync(
        string graphBaseUrl,
        string phoneNumberId,
        string accessToken,
        int timeoutSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>Registers callbackUrl as the WABA-level webhook override via Meta's
    /// POST /{WABA_ID}/subscribed_apps (blueprint §4.3) — what makes a saved, validated WhatsApp
    /// connection actually receive Meta's traffic without the operator touching Meta's console
    /// themselves. Requires the access token to carry whatsapp_business_management scope; a
    /// missing-scope rejection is reported back as SafeError, not thrown.</summary>
    Task<WhatsAppWebhookRegistrationResult> RegisterWebhookAsync(
        string graphBaseUrl,
        string wabaId,
        string accessToken,
        string callbackUrl,
        string verifyToken,
        int timeoutSeconds,
        CancellationToken cancellationToken = default);
}

public sealed record WhatsAppGraphValidationResult(bool Success, string? DisplayPhoneNumber, string? VerifiedName, string? SafeError);

public sealed record WhatsAppWebhookRegistrationResult(bool Success, string? SafeError);
