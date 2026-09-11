using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// The category of external service a tenant can connect. Each type has exactly one active
/// connection per tenant workspace at a time — connecting a new provider for the same type
/// replaces the previous one, mirroring how WhatsAppChannelConfiguration already works for
/// WhatsApp specifically. See docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md §3.
/// </summary>
public enum ConnectionType
{
    Sms,
    Email,
}

/// <summary>
/// A tenant-configured connection to an external provider (SMS gateway, email sender, ...).
/// Generalizes the pattern WhatsAppChannelConfiguration pioneered: credentials entered through a
/// settings screen, validated against the real provider before anything is persisted, encrypted
/// at rest, and never returned to a browser once saved (see ConnectionEndpoints.GetStatus, which
/// mirrors WhatsAppConfigurationEndpoints.GetStatus — booleans and non-secret metadata only).
/// </summary>
public sealed record IntegrationConnection
{
    public required TenantScope Scope { get; init; }
    public required ConnectionType ConnectionType { get; init; }
    public required string ProviderKey { get; init; }

    /// <summary>Provider-specific secret fields (credentials), JSON-serialized as one object then
    /// encrypted as a single blob via IConnectionSecretProtector. Never returned to a browser once
    /// saved — only whether it is present.</summary>
    public required string EncryptedSecretSettingsJson { get; init; }

    /// <summary>Provider-specific non-secret fields (sender id, base url, display name, sandbox
    /// flag) as plain JSON — safe to read back and display without decryption.</summary>
    public string? NonSecretSettingsJson { get; init; }

    public bool IsActive { get; init; } = true;
    public required string ConfiguredByUserId { get; init; }
    public required DateTimeOffset ConfiguredAt { get; init; }
    public DateTimeOffset? LastValidatedAt { get; init; }
    public string? LastValidationStatus { get; init; }
    public string? LastValidationError { get; init; }

    /// <summary>Non-secret facts the last validation discovered (e.g. current SMS balance, a
    /// verified display name) — safe to show in the connections screen.</summary>
    public string? LastValidationDiscoveredJson { get; init; }

    /// <summary>Deterministic per-(tenant, connection type) id — same DeterministicId pattern as
    /// WhatsAppChannelConfiguration.StorageKey. Saving a new provider for the same connection type
    /// overwrites the previous one: one active connection per type, not an accumulating list.</summary>
    public static Guid StorageKey(TenantScope scope, ConnectionType connectionType)
    {
        var key = $"{scope.TenantId}:{scope.WorkspaceId}:integration-connection:{connectionType}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}

public interface IIntegrationConnectionRepository
{
    Task<IntegrationConnection?> GetAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default);

    Task SaveAsync(IntegrationConnection connection, CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default);
}

/// <summary>Encrypts/decrypts connection secret fields at rest, generalized from
/// IWhatsAppCredentialProtector. Implemented over ASP.NET Core Data Protection (a real,
/// key-rotation-capable, deployment-scoped protector) — never a hand-rolled cipher, never hashing
/// (the plaintext must be recoverable to actually call the provider).</summary>
public interface IConnectionSecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}

/// <summary>Proves a connection's credentials actually work against the real provider before
/// anything is persisted — the same guarantee WhatsAppConfigurationEndpoints already provides via
/// IWhatsAppGraphValidationClient, generalized to every connection type and provider. One
/// validator per (ConnectionType, ProviderKey) pair.</summary>
public interface IConnectionValidator
{
    ConnectionType ConnectionType { get; }

    string ProviderKey { get; }

    Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default);
}

public sealed record ConnectionValidationResult(
    bool Success,
    string? SafeError,
    IReadOnlyDictionary<string, string>? DiscoveredSettings = null);

/// <summary>
/// Splits a flat settings dictionary into secret vs non-secret fields for storage, and merges
/// them back into one dictionary for use. Pure JSON plumbing shared by every connection type —
/// no Infrastructure dependency, so it lives in Domain alongside IntegrationConnection.
/// </summary>
public static class ConnectionSettingsCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static (string EncryptedSecretSettingsJson, string? NonSecretSettingsJson) Encode(
        IConnectionSecretProtector protector,
        IReadOnlyDictionary<string, string> secretFields,
        IReadOnlyDictionary<string, string>? nonSecretFields)
    {
        var secretJson = JsonSerializer.Serialize(secretFields, JsonOptions);
        var encryptedSecretJson = protector.Protect(secretJson);
        var nonSecretJson = nonSecretFields is { Count: > 0 }
            ? JsonSerializer.Serialize(nonSecretFields, JsonOptions)
            : null;
        return (encryptedSecretJson, nonSecretJson);
    }

    /// <summary>Merges decrypted secret fields with non-secret fields into one dictionary for a
    /// provider factory to consume. Secret fields win on key collision.</summary>
    public static IReadOnlyDictionary<string, string> Decode(
        IConnectionSecretProtector protector,
        IntegrationConnection connection)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(connection.NonSecretSettingsJson))
        {
            var nonSecret = JsonSerializer.Deserialize<Dictionary<string, string>>(connection.NonSecretSettingsJson, JsonOptions);
            if (nonSecret is not null)
            {
                foreach (var (key, value) in nonSecret) merged[key] = value;
            }
        }

        var secretJson = protector.Unprotect(connection.EncryptedSecretSettingsJson);
        var secret = JsonSerializer.Deserialize<Dictionary<string, string>>(secretJson, JsonOptions);
        if (secret is not null)
        {
            foreach (var (key, value) in secret) merged[key] = value;
        }

        return merged;
    }

    /// <summary>Non-secret fields only, decoded without touching the protector — safe to call when
    /// displaying a connection's status (e.g. sender id, base url) without decrypting anything.</summary>
    public static IReadOnlyDictionary<string, string> DecodeNonSecret(IntegrationConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.NonSecretSettingsJson))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(connection.NonSecretSettingsJson, JsonOptions)
            ?? new Dictionary<string, string>();
    }

    public static string? EncodeDiscovered(IReadOnlyDictionary<string, string>? discovered) =>
        discovered is { Count: > 0 } ? JsonSerializer.Serialize(discovered, JsonOptions) : null;
}
