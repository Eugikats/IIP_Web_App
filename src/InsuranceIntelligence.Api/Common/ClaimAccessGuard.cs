using InsuranceIntelligence.Api.Auth;
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

    private static bool Matches(string userId, string? candidate) =>
        !string.IsNullOrWhiteSpace(userId)
        && !string.IsNullOrWhiteSpace(candidate)
        && string.Equals(userId.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase);
}
