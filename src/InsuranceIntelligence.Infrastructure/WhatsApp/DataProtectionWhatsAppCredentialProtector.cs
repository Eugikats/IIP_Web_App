using InsuranceIntelligence.Domain.Integrations;
using Microsoft.AspNetCore.DataProtection;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// Real, reversible encryption for WhatsApp secrets at rest, not hashing, which cannot be
/// undone and would make the access token unusable for the outbound Graph API calls it exists to
/// authorize. Data Protection's key ring is the actual secret; loss of it (or running on a
/// different, unconfigured deployment) makes existing ciphertext unrecoverable by design, which is
/// why WhatsAppConfigurationEndpoints always re-validates against the real Meta API rather than
/// trusting an opaque "still configured" flag.
/// </summary>
public sealed class DataProtectionWhatsAppCredentialProtector : IWhatsAppCredentialProtector
{
    private readonly IDataProtector protector;

    public DataProtectionWhatsAppCredentialProtector(IDataProtectionProvider dataProtectionProvider)
    {
        protector = dataProtectionProvider.CreateProtector("InsuranceIntelligence.WhatsAppChannelCredentials.v1");
    }

    public string Protect(string plaintext) => protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => protector.Unprotect(ciphertext);
}
