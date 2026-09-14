using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.Core;

public interface IPermissionReadRepository
{
    Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        string tenantId,
        string workspaceId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the authenticated user's effective IIP membership from the runtime authority
    /// (membership/role/permission store), keyed by the user's stable id (Entra <c>oid</c>). The
    /// tenant/workspace hints (from the token's <c>tid</c> / an IIP-issued claim) are only used to
    /// disambiguate when a user belongs to more than one tenant/workspace, they never grant access.
    /// Returns <c>null</c> when the user has no active membership for the resolved scope (fail closed).
    /// See ADR-0005.
    /// </summary>
    Task<EffectiveMembership?> GetEffectiveMembershipAsync(
        string userId,
        string? tenantHint,
        string? workspaceHint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The server-resolved authority for a request: which tenant/workspace the authenticated user
/// actually belongs to, and the fine-grained permissions they hold there. Never derived from the
/// token's role/tid claims (ADR-0005).
/// </summary>
public sealed record EffectiveMembership(
    string TenantId,
    string WorkspaceId,
    string UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<Permission> Permissions);

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default);

    Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default);
}

public sealed class PermissionDeniedException(Permission permission)
    : InvalidOperationException($"Permission '{permission}' is required.")
{
    public Permission Permission { get; } = permission;
}

public sealed class PermissionService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionReadRepository permissionReadRepository) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var persistedPermissions = await permissionReadRepository.GetPermissionsAsync(
            context.TenantId,
            context.WorkspaceId,
            context.UserId,
            cancellationToken);

        if (persistedPermissions.Contains(permission))
        {
            return true;
        }

        foreach (var roleCode in context.RoleCodes)
        {
            if (Enum.TryParse<PlatformRole>(roleCode, ignoreCase: true, out var platformRole)
                && RolePermissionMap.HasPermission(platformRole, permission))
            {
                return true;
            }
        }

        return false;
    }

    public async Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(permission, cancellationToken))
        {
            throw new PermissionDeniedException(permission);
        }
    }
}
