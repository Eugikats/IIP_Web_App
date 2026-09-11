using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.LocalTestDoubles;

/// <summary>Reversible, non-secure stand-in for Data Protection in tests — never use outside
/// tests. Prefixes so a test can assert a value was actually run through Protect/Unprotect rather
/// than stored as plaintext by mistake.</summary>
public sealed class FakeWhatsAppCredentialProtector : IWhatsAppCredentialProtector
{
    private const string Prefix = "protected:";

    public string Protect(string plaintext) => Prefix + plaintext;

    public string Unprotect(string ciphertext) =>
        ciphertext.StartsWith(Prefix, StringComparison.Ordinal) ? ciphertext[Prefix.Length..] : ciphertext;
}

/// <summary>Scripted IWhatsAppGraphValidationClient — never calls the real Meta API.</summary>
public sealed class FakeWhatsAppGraphValidationClient(
    WhatsAppGraphValidationResult result,
    WhatsAppWebhookRegistrationResult? webhookResult = null) : IWhatsAppGraphValidationClient
{
    public List<(string PhoneNumberId, string AccessToken)> Calls { get; } = [];
    public List<(string WabaId, string CallbackUrl, string VerifyToken)> WebhookRegistrationCalls { get; } = [];

    public Task<WhatsAppGraphValidationResult> ValidateAsync(
        string graphBaseUrl, string phoneNumberId, string accessToken, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        Calls.Add((phoneNumberId, accessToken));
        return Task.FromResult(result);
    }

    public Task<WhatsAppWebhookRegistrationResult> RegisterWebhookAsync(
        string graphBaseUrl, string wabaId, string accessToken, string callbackUrl, string verifyToken, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        WebhookRegistrationCalls.Add((wabaId, callbackUrl, verifyToken));
        return Task.FromResult(webhookResult ?? new WhatsAppWebhookRegistrationResult(true, null));
    }
}
