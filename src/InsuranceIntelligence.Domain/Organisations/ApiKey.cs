namespace InsuranceIntelligence.Domain.Organisations;

/// <summary>An API key for one organisation's own systems. Only a hash of the key is kept, so it
/// cannot be read back; the key is shown once, when it is made.</summary>
public sealed record ApiKeyRecord(
    Guid KeyId,
    string OrganisationId,
    string Name,
    string Prefix,
    string KeyHash,
    string UserId,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? LastUsedAt = null,
    DateTimeOffset? RevokedAt = null)
{
    public const string UserPrefix = "apikey:";
    public const string KeyPrefix = "iip_";

    public bool IsUsable(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

    /// <summary>True for the identity an API key acts as; such an identity is never a person.</summary>
    public static bool IsMachineUser(string? userId) => userId?.StartsWith(UserPrefix, StringComparison.Ordinal) == true;
}

public interface IApiKeyRepository
{
    Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApiKeyRecord key, CancellationToken cancellationToken = default);
    Task<ApiKeyRecord?> GetByHashAsync(string keyHash, CancellationToken cancellationToken = default);
    Task<ApiKeyRecord?> GetAsync(string organisationId, Guid keyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiKeyRecord>> ListAsync(string organisationId, CancellationToken cancellationToken = default);
}
