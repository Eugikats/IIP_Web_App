using InsuranceIntelligence.Domain.Integrations;
using Microsoft.AspNetCore.DataProtection;

namespace InsuranceIntelligence.Infrastructure.Connections;

/// <summary>
/// Real, reversible encryption for integration connection secrets at rest, not hashing, which
/// cannot be undone and would make credentials unusable for the outbound calls they exist to
/// authorize. Same mechanism as DataProtectionWhatsAppCredentialProtector, under its own purpose
/// string so the two protectors derive independent keys from the same Data Protection key ring.
/// Loss of the key ring (or running on a different, unconfigured deployment) makes existing
/// ciphertext unrecoverable by design, which is why every connection is re-validated against the
/// real provider before being trusted, never assumed valid from an opaque "still configured" flag.
/// </summary>
public sealed class DataProtectionConnectionSecretProtector : IConnectionSecretProtector
{
    private readonly IDataProtector protector;

    public DataProtectionConnectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        protector = dataProtectionProvider.CreateProtector("InsuranceIntelligence.IntegrationConnectionSecrets.v1");
    }

    public string Protect(string plaintext) => protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => protector.Unprotect(ciphertext);
}
