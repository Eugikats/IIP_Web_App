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
public sealed class FakeWhatsAppGraphValidationClient(WhatsAppGraphValidationResult result) : IWhatsAppGraphValidationClient
{
    public List<(string PhoneNumberId, string AccessToken)> Calls { get; } = [];

    public Task<WhatsAppGraphValidationResult> ValidateAsync(
        string graphBaseUrl, string phoneNumberId, string accessToken, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        Calls.Add((phoneNumberId, accessToken));
        return Task.FromResult(result);
    }
}
