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
}

public sealed record WhatsAppGraphValidationResult(bool Success, string? DisplayPhoneNumber, string? VerifiedName, string? SafeError);
