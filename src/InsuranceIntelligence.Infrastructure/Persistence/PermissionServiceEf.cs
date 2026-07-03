using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class PermissionServiceEf(
    InsuranceIntelligenceDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;

        var hasMembership = await dbContext.UserTenantMemberships.AsNoTracking()
            .AnyAsync(item =>
                item.TenantId == context.TenantId
                && item.WorkspaceId == context.WorkspaceId
                && item.UserId == context.UserId
                && item.IsActive,
                cancellationToken);

        if (!hasMembership)
        {
            return false;
        }

        var roleCodes = await dbContext.RoleAssignments.AsNoTracking()
            .Where(item =>
                item.TenantId == context.TenantId
                && item.WorkspaceId == context.WorkspaceId
                && item.UserId == context.UserId)
            .Select(item => item.RoleCode)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (roleCodes.Length == 0)
        {
            roleCodes = context.RoleCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var requestedPermissionCode = permission.ToString();
        var hasPersistedPermission = await dbContext.RolePermissions.AsNoTracking()
            .AnyAsync(item =>
                item.TenantId == context.TenantId
                && item.WorkspaceId == context.WorkspaceId
                && roleCodes.Contains(item.RoleCode)
                && item.PermissionCode == requestedPermissionCode,
                cancellationToken);

        if (hasPersistedPermission)
        {
            return true;
        }

        return roleCodes.Any(roleCode =>
            Enum.TryParse<PlatformRole>(roleCode, ignoreCase: true, out var platformRole)
            && RolePermissionMap.HasPermission(platformRole, permission));
    }

    public async Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(permission, cancellationToken))
        {
            throw new PermissionDeniedException(permission);
        }
    }
}
