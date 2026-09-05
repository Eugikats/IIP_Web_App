using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// Tenant-supplied WhatsApp Business API credentials, configured through the settings screen
/// instead of a deployment environment variable. AccessToken, AppSecret, and VerifyToken are
/// never stored in plain text — only the ciphertext produced by IWhatsAppCredentialProtector
/// (backed by ASP.NET Core Data Protection) is persisted, and the plaintext values are never
/// sent back to a browser once saved (see WhatsAppConfigurationEndpoints.GetStatus, which returns
/// only booleans and non-secret metadata).
///
/// One row per tenant: this edition of IIP deploys one Managed Application per insurer, so a
/// tenant's WhatsApp Business number is a singleton, not a list.
/// </summary>
public sealed record WhatsAppChannelConfiguration
{
    public required TenantScope Scope { get; init; }
    public required string PhoneNumberId { get; init; }
    public string? BusinessDisplayName { get; init; }
    public required string EncryptedAccessToken { get; init; }
    public required string EncryptedAppSecret { get; init; }
    public required string EncryptedVerifyToken { get; init; }
    public bool IsActive { get; init; } = true;
    public required string ConfiguredByUserId { get; init; }
    public required DateTimeOffset ConfiguredAt { get; init; }
    public DateTimeOffset? LastValidatedAt { get; init; }
    public string? LastValidationStatus { get; init; }
    public string? LastValidationError { get; init; }

    /// <summary>Deterministic per-tenant id, same DeterministicId pattern used elsewhere in this
    /// codebase — a tenant saving new credentials always updates its one row rather than
    /// accumulating duplicates.</summary>
    public static Guid StorageKey(TenantScope scope)
    {
        var key = $"{scope.TenantId}:{scope.WorkspaceId}:whatsapp-channel-configuration";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}

public interface IWhatsAppChannelConfigurationRepository
{
    Task<WhatsAppChannelConfiguration?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(WhatsAppChannelConfiguration configuration, CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

/// <summary>Encrypts/decrypts WhatsApp secret fields at rest. Implemented over ASP.NET Core Data
/// Protection (a real, key-rotation-capable, tenant-of-the-machine/deployment-scoped protector) —
/// never a hand-rolled cipher.</summary>
public interface IWhatsAppCredentialProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}
