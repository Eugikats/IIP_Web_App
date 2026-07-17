using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Common;

public static class ClaimAccessGuard
{
    public static IReadOnlyCollection<ClaimCase> FilterVisibleCases(
        HttpContext httpContext,
        IReadOnlyCollection<ClaimCase> cases) =>
        cases
            .Where(claimCase => CanViewCase(httpContext, claimCase))
            .ToArray();

    public static bool CanViewCase(HttpContext httpContext, ClaimCase claimCase)
    {
        if (TryGetCurrentTenantContext(httpContext, out var tenantContext))
        {
            if (HasContextPermission(tenantContext, Permission.ViewAllClaims))
            {
                return true;
            }

            return HasContextPermission(tenantContext, Permission.ViewAssignedClaims)
                && (Matches(tenantContext.UserId, claimCase.AssignedUserId)
                    || Matches(tenantContext.UserId, claimCase.CreatedByUserId));
        }

        var role = DemoAuthorization.GetEffectiveRole(httpContext, ResolveOptions(httpContext));
        if (RolePermissionMap.HasPermission(role, Permission.ViewAllClaims))
        {
            return true;
        }

        if (!RolePermissionMap.HasPermission(role, Permission.ViewAssignedClaims))
        {
            return false;
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        return Matches(userId, claimCase.AssignedUserId)
            || Matches(userId, claimCase.CreatedByUserId);
    }

    private static InfrastructureOptions ResolveOptions(HttpContext httpContext)
    {
        try
        {
            return httpContext.RequestServices.GetService<InfrastructureOptions>()
                ?? new InfrastructureOptions { AuthMode = "Development" };
        }
        catch (ArgumentNullException)
        {
            return new InfrastructureOptions { AuthMode = "Development" };
        }
        catch (InvalidOperationException)
        {
            return new InfrastructureOptions { AuthMode = "Development" };
        }
    }

    private static bool TryGetCurrentTenantContext(HttpContext httpContext, out TenantContext tenantContext)
    {
        try
        {
            if (httpContext.RequestServices.GetService<ITenantContextAccessor>() is { HasCurrent: true } accessor)
            {
                tenantContext = accessor.Current;
                return true;
            }
        }
        catch (ArgumentNullException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        tenantContext = default!;
        return false;
    }

    private static bool HasContextPermission(TenantContext tenantContext, Permission permission) =>
        tenantContext.RoleCodes
            .Select(role => Enum.TryParse<PlatformRole>(role, ignoreCase: true, out var platformRole)
                ? platformRole
                : (PlatformRole?)null)
            .Where(role => role.HasValue)
            .Any(role => RolePermissionMap.HasPermission(role!.Value, permission));

    private static bool Matches(string userId, string? candidate) =>
        !string.IsNullOrWhiteSpace(userId)
        && !string.IsNullOrWhiteSpace(candidate)
        && string.Equals(userId.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase);
}
