using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.LocalTestDoubles;

/// <summary>Reversible, non-secure stand-in for Data Protection in tests, never use outside
/// tests. Prefixes so a test can assert a value was actually run through Protect/Unprotect rather
/// than stored as plaintext by mistake.</summary>
public sealed class FakeWhatsAppCredentialProtector : IWhatsAppCredentialProtector
{
    private const string Prefix = "protected:";

    public string Protect(string plaintext) => Prefix + plaintext;

    public string Unprotect(string ciphertext) =>
        ciphertext.StartsWith(Prefix, StringComparison.Ordinal) ? ciphertext[Prefix.Length..] : ciphertext;
}

/// <summary>Scripted IWhatsAppGraphValidationClient, never calls the real Meta API.</summary>
public sealed class FakeWhatsAppGraphValidationClient(
    WhatsAppGraphValidationResult result,
    WhatsAppWebhookRegistrationResult? webhookResult = null,
    // Real Meta calls this same webhook right back, synchronously, as part of accepting the
    // registration request, before RegisterWebhookAsync's own HTTP call even returns. This hook
    // lets a test observe exactly what state the caller had already committed at that instant,
    // the same thing that was missing when this endpoint asked Meta to register before saving.
    Func<Task>? onRegisterWebhook = null) : IWhatsAppGraphValidationClient
{
    public List<(string PhoneNumberId, string AccessToken)> Calls { get; } = [];
    public List<(string WabaId, string CallbackUrl, string VerifyToken)> WebhookRegistrationCalls { get; } = [];

    /// <summary>What the fake reports Meta says about delivery. A healthy, unremarkable setup by default.</summary>
    public WhatsAppMetaFacts MetaFacts { get; set; } = new(null, "CONNECTED", "LIVE", null, null, null, []);

    public Task<WhatsAppMetaFacts> ReadDeliverySetupAsync(
        string graphBaseUrl, string phoneNumberId, string wabaId, string accessToken, int timeoutSeconds, CancellationToken cancellationToken = default) =>
        Task.FromResult(MetaFacts);

    public Task<WhatsAppGraphValidationResult> ValidateAsync(
        string graphBaseUrl, string phoneNumberId, string accessToken, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        Calls.Add((phoneNumberId, accessToken));
        return Task.FromResult(result);
    }

    public async Task<WhatsAppWebhookRegistrationResult> RegisterWebhookAsync(
        string graphBaseUrl, string wabaId, string accessToken, string callbackUrl, string verifyToken, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        WebhookRegistrationCalls.Add((wabaId, callbackUrl, verifyToken));
        if (onRegisterWebhook is not null)
        {
            await onRegisterWebhook();
        }
        return webhookResult ?? new WhatsAppWebhookRegistrationResult(true, null);
    }
}
