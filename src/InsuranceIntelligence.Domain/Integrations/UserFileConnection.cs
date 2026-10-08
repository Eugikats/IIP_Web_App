using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>The cloud file services a person can connect their own account to.</summary>
public enum FileProvider
{
    Google,
    Microsoft,
}

/// <summary>
/// One person's connection to their own Google Drive or Microsoft OneDrive and SharePoint account.
/// It belongs to the person, not the organisation: what IIP can open is only what that person can open,
/// and one colleague's connection never lets another read their files. The refresh token is encrypted at
/// rest and never leaves the API; the browser is only ever told whether a connection exists and for which
/// account.
/// </summary>
public sealed record UserFileConnection
{
    public required TenantScope Scope { get; init; }
    public required string UserId { get; init; }
    public required FileProvider Provider { get; init; }
    public string? AccountEmail { get; init; }
    public required string ProtectedRefreshToken { get; init; }
    public required string GrantedScopes { get; init; }
    public required DateTimeOffset ConnectedAt { get; init; }

    public static Guid StorageKey(TenantScope scope, string userId, FileProvider provider)
    {
        var key = $"{scope.TenantId}:{scope.WorkspaceId}:file-connection:{provider}:{userId.ToLowerInvariant()}";
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(key)));
    }
}

public interface IUserFileConnectionRepository
{
    Task<UserFileConnection?> GetAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default);

    Task SaveAsync(UserFileConnection connection, CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default);
}
