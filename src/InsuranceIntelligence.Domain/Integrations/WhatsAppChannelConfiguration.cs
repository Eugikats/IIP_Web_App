using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// Tenant-supplied WhatsApp Business API credentials, configured through the settings screen
/// instead of a deployment environment variable. AccessToken, AppSecret, and VerifyToken are
/// never stored in plain text, only the ciphertext produced by IWhatsAppCredentialProtector
/// (backed by ASP.NET Core Data Protection) is persisted, and the plaintext values are never
/// sent back to a browser once saved (see WhatsAppConfigurationEndpoints.GetStatus, which returns
/// only booleans and non-secret metadata).
///
/// One row per workspace: a workspace has one WhatsApp Business number. Several workspaces on one
/// deployment each have their own row, and an inbound webhook is routed to the workspace whose row
/// holds the phone number it names (build-loop item 27). A phone number can belong to one workspace only.
/// </summary>
public sealed record WhatsAppChannelConfiguration
{
    public required TenantScope Scope { get; init; }
    public required string PhoneNumberId { get; init; }

    /// <summary>The WhatsApp Business Account id, from Meta Business Settings next to the phone
    /// number, required to register the webhook via subscribed_apps (blueprint §4.3). Optional
    /// on the record so existing saved configurations (from before this field existed) still
    /// deserialize; SaveConfiguration requires it for new/updated connections.</summary>
    public string? WabaId { get; init; }
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

    /// <summary>The callback URL registered with Meta, shown in the settings screen so the
    /// operator has the exact value if auto-registration ever needs to be redone manually.</summary>
    public string? WebhookCallbackUrl { get; init; }

    /// <summary>"Registered" | "RegistrationFailed" | "ManualRequired" | null (never attempted).
    /// See WhatsAppConfigurationEndpoints.SaveConfiguration.</summary>
    public string? WebhookRegistrationStatus { get; init; }
    public string? WebhookRegistrationError { get; init; }

    /// <summary>Deterministic per-tenant id, same DeterministicId pattern used elsewhere in this
    /// codebase, a tenant saving new credentials always updates its one row rather than
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

    /// <summary>Every active connection in every workspace. Deliberately not scoped: a webhook from Meta
    /// names only a phone number, and the number is what says which workspace it belongs to (build-loop
    /// item 27). Used only by the routing snapshot (WhatsAppRuntimeSettingsProvider) and the check that a
    /// number is saved by one workspace at most; never returned to a caller or a browser.</summary>
    Task<IReadOnlyList<WhatsAppChannelConfiguration>> ListActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Encrypts/decrypts WhatsApp secret fields at rest. Implemented over ASP.NET Core Data
/// Protection (a real, key-rotation-capable, tenant-of-the-machine/deployment-scoped protector),
/// never a hand-rolled cipher.</summary>
public interface IWhatsAppCredentialProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}
