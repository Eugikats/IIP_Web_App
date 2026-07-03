using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Auth;

public static class DemoAuthorization
{
    public static IResult? Require(HttpContext httpContext, InfrastructureOptions options, Permission permission)
    {
        return RequireAny(httpContext, options, [permission]);
    }

    public static IResult? RequireAny(
        HttpContext httpContext,
        InfrastructureOptions options,
        IReadOnlyCollection<Permission> permissions)
    {
        if (!options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (HasDemoIdentityHeaders(httpContext))
            {
                return Results.Problem(
                    "Demo identity headers are not accepted outside Development auth mode.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var authenticatedRole = GetAuthenticatedRole(httpContext);
            return permissions.Any(permission => RolePermissionMap.HasPermission(authenticatedRole, permission))
                ? null
                : Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var role = GetRole(httpContext);
        return permissions.Any(permission => RolePermissionMap.HasPermission(role, permission))
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    public static PlatformRole GetRole(HttpContext httpContext)
    {
        // Least-privilege default: a missing or unrecognized demo role must never
        // silently escalate to Admin. Callers in Development mode send an explicit
        // role; anything else falls back to the most restricted platform role.
        var roleValue = httpContext.Request.Headers.TryGetValue("x-demo-role", out var demoRole)
            ? demoRole.ToString()
            : httpContext.Request.Headers.TryGetValue("X-Demo-Role", out var legacyDemoRole)
                ? legacyDemoRole.ToString()
                : null;

        return Enum.TryParse<PlatformRole>(roleValue, true, out var role) ? role : PlatformRole.Client;
    }

    public static PlatformRole GetEffectiveRole(HttpContext httpContext, InfrastructureOptions options) =>
        !options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && httpContext.User.Identity?.IsAuthenticated == true
            ? GetAuthenticatedRole(httpContext)
            : GetRole(httpContext);

    public static string GetUserId(HttpContext httpContext, string fallback = "local-admin")
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var authenticatedUserId = FirstClaimValue(httpContext, RuntimeClaimTypes.UserIds);
            return !string.IsNullOrWhiteSpace(authenticatedUserId) ? authenticatedUserId : fallback;
        }

        if (httpContext.Request.Headers.TryGetValue("x-demo-user-id", out var demoUserId)
            && !string.IsNullOrWhiteSpace(demoUserId))
        {
            return demoUserId.ToString();
        }

        if (httpContext.Request.Headers.TryGetValue("X-User-Id", out var userId)
            && !string.IsNullOrWhiteSpace(userId))
        {
            return userId.ToString();
        }

        return fallback;
    }

    private static PlatformRole GetAuthenticatedRole(HttpContext httpContext)
    {
        foreach (var claimType in RuntimeClaimTypes.Roles)
        {
            foreach (var claim in httpContext.User.FindAll(claimType))
            {
                if (Enum.TryParse<PlatformRole>(claim.Value, ignoreCase: true, out var role))
                {
                    return role;
                }
            }
        }

        return PlatformRole.Client;
    }

    private static string? FirstClaimValue(HttpContext httpContext, IReadOnlyCollection<string> claimTypes) =>
        claimTypes
            .Select(type => httpContext.User.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool HasDemoIdentityHeaders(HttpContext httpContext) =>
        httpContext.Request.Headers.ContainsKey("x-demo-role")
        || httpContext.Request.Headers.ContainsKey("X-Demo-Role")
        || httpContext.Request.Headers.ContainsKey("x-demo-user-id")
        || httpContext.Request.Headers.ContainsKey("X-User-Id");
}
