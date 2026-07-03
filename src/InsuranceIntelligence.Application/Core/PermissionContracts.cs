using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.Core;

public interface IPermissionReadRepository
{
    Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        string tenantId,
        string workspaceId,
        string userId,
        CancellationToken cancellationToken = default);
}

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
