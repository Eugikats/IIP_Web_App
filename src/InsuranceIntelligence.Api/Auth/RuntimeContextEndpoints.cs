using System.Security.Claims;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Tenants;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Auth;

public static class RuntimeContextEndpoints
{
    public static async Task<IResult> GetMe(
        HttpContext httpContext,
        InfrastructureOptions options,
        IHostEnvironment hostEnvironment,
        IWorkspaceCatalogService workspaceCatalog,
        CancellationToken cancellationToken)
    {
        var environment = EnvironmentContextFactory.Build(options, hostEnvironment.EnvironmentName, source: "Runtime");
        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var developmentScope, out var validationError))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["scope"] = [validationError]
                });
            }

            var developmentRole = DemoAuthorization.GetRole(httpContext);
            var developmentUserId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
            return Results.Ok(BuildResponse(
                userId: developmentUserId,
                displayName: string.IsNullOrWhiteSpace(developmentUserId) ? null : developmentUserId,
                role: developmentRole,
                scope: developmentScope,
                authMode: options.AuthMode,
                source: "DevelopmentHeaders",
                environment: environment,
                availableWorkspaces: await ResolveWorkspacesAsync(workspaceCatalog, developmentScope, cancellationToken)));
        }

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

        var scopeSource = TenantScopeReader.TryGetFromClaims(httpContext.User, out _, out _)
            ? "AuthenticatedClaims"
            : "DeploymentConfiguration";
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var authenticatedScope, out _))
        {
            return Results.Problem(
                "Authenticated identity is missing a valid tenant/workspace scope and no deployment scope is configured.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var userId = FirstClaimValue(httpContext.User, RuntimeClaimTypes.UserIds);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                "Authenticated identity is missing a stable user identifier.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var role = GetRoleFromClaims(httpContext.User);
        var displayName = FirstClaimValue(httpContext.User, RuntimeClaimTypes.DisplayNames) ?? userId;
        return Results.Ok(BuildResponse(
            userId: userId,
            displayName: displayName,
            role: role,
            scope: authenticatedScope,
            authMode: options.AuthMode,
            source: scopeSource,
            environment: environment,
            availableWorkspaces: await ResolveWorkspacesAsync(workspaceCatalog, authenticatedScope, cancellationToken)));
    }

    private static RuntimeContextResponse BuildResponse(
        string userId,
        string? displayName,
        PlatformRole role,
        TenantScope scope,
        string authMode,
        string source,
        UiEnvironmentContext environment,
        IReadOnlyList<WorkspaceSummaryDto> availableWorkspaces) =>
        new(
            UserId: userId,
            DisplayName: displayName,
            Role: role.ToString(),
            RoleLabel: RoleLabel(role),
            Permissions: RolePermissionMap.ForRole(role).Select(permission => permission.ToString()).Order().ToArray(),
            TenantId: scope.TenantId,
            WorkspaceId: scope.WorkspaceId,
            AuthMode: authMode,
            Source: source,
            Environment: environment,
            AvailableWorkspaces: availableWorkspaces);

    // Multi-workspace data model: return the tenant's catalog if any workspaces are registered;
    // otherwise fall back honestly to the caller's single resolved workspace (one-per-instance).
    private static async Task<IReadOnlyList<WorkspaceSummaryDto>> ResolveWorkspacesAsync(
        IWorkspaceCatalogService catalog,
        TenantScope scope,
        CancellationToken cancellationToken)
    {
        var catalogued = await catalog.ListForTenantAsync(scope.TenantId, cancellationToken);
        if (catalogued.Count > 0)
        {
            return catalogued;
        }

        return string.IsNullOrWhiteSpace(scope.WorkspaceId)
            ? []
            : [new WorkspaceSummaryDto(scope.WorkspaceId, scope.WorkspaceId, "default")];
    }

    private static bool HasDemoIdentityHeaders(HttpContext httpContext) =>
        httpContext.Request.Headers.ContainsKey("x-demo-role")
        || httpContext.Request.Headers.ContainsKey("X-Demo-Role")
        || httpContext.Request.Headers.ContainsKey("x-demo-user-id");

    private static PlatformRole GetRoleFromClaims(ClaimsPrincipal user)
    {
        foreach (var claimType in RuntimeClaimTypes.Roles)
        {
            foreach (var claim in user.FindAll(claimType))
            {
                if (Enum.TryParse<PlatformRole>(claim.Value, ignoreCase: true, out var role))
                {
                    return role;
                }
            }
        }

        return PlatformRole.Client;
    }

    private static string? FirstClaimValue(ClaimsPrincipal user, IReadOnlyCollection<string> claimTypes) =>
        claimTypes
            .Select(type => user.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string RoleLabel(PlatformRole role) =>
        role switch
        {
            PlatformRole.ClaimsOfficer => "Claims Handler",
            PlatformRole.ClaimsSupervisor => "Claims Manager",
            PlatformRole.CustomerService => "Customer Service Agent",
            PlatformRole.ITAdmin => "Integration Admin",
            PlatformRole.Compliance => "Compliance Auditor",
            PlatformRole.Admin => "Tenant Admin",
            _ => role.ToString()
        };
}

public sealed record RuntimeContextResponse(
    string UserId,
    string? DisplayName,
    string Role,
    string RoleLabel,
    IReadOnlyCollection<string> Permissions,
    string TenantId,
    string WorkspaceId,
    string AuthMode,
    string Source,
    UiEnvironmentContext Environment,
    IReadOnlyList<WorkspaceSummaryDto> AvailableWorkspaces);
