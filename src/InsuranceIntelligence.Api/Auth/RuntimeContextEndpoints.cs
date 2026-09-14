using System.Security.Claims;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Core;
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
        ITenantContextAccessor tenantContext,
        CancellationToken cancellationToken,
        IUserAccountRepository? userAccounts = null)
    {
        var environment = EnvironmentContextFactory.Build(options, hostEnvironment.EnvironmentName, source: "Runtime");
        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (tenantContext.HasCurrent)
            {
                var developmentContext = tenantContext.Current;
                var currentScope = TenantScope.Create(developmentContext.TenantId, developmentContext.WorkspaceId);
                var currentRole = developmentContext.RoleCodes
                    .Select(code => Enum.TryParse<PlatformRole>(code, ignoreCase: true, out var parsed) ? parsed : (PlatformRole?)null)
                    .OfType<PlatformRole>()
                    .DefaultIfEmpty(PlatformRole.Client)
                    .First();
                return Results.Ok(BuildResponse(
                    userId: developmentContext.UserId,
                    displayName: developmentContext.UserId,
                    role: currentRole,
                    scope: currentScope,
                    authMode: options.AuthMode,
                    source: "DevelopmentTenantContext",
                    environment: environment,
                    availableWorkspaces: await ResolveWorkspacesAsync(workspaceCatalog, currentScope, cancellationToken)));
            }

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

        // ADR-0005: outside Development, the runtime authority is IIP's membership store, resolved by
        // the per-request middleware into the TenantContext. Never derive tenant/workspace/role from
        // the token's tid/roles claims here. No active membership => fail closed.
        if (!tenantContext.HasCurrent)
        {
            return Results.Problem(
                "Authenticated identity has no active IIP membership for this request.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var current = tenantContext.Current;
        var scope = TenantScope.Create(current.TenantId, current.WorkspaceId);
        var role = current.RoleCodes
            .Select(code => Enum.TryParse<PlatformRole>(code, ignoreCase: true, out var parsed) ? parsed : (PlatformRole?)null)
            .OfType<PlatformRole>()
            .DefaultIfEmpty(PlatformRole.Client)
            .First();
        var displayName = FirstClaimValue(httpContext.User, RuntimeClaimTypes.DisplayNames) ?? current.UserId;
        var email = httpContext.User.FindFirst("preferred_username")?.Value
            ?? httpContext.User.FindFirst("email")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        await RecordSignInAsync(userAccounts, scope, current.UserId, displayName, email, role, cancellationToken);
        return Results.Ok(BuildResponse(
            userId: current.UserId,
            displayName: displayName,
            role: role,
            scope: scope,
            authMode: options.AuthMode,
            source: "MembershipStore",
            environment: environment,
            availableWorkspaces: await ResolveWorkspacesAsync(workspaceCatalog, scope, cancellationToken)));
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

    /// <summary>
    /// Records each person the first time they use the app, and keeps their name current after
    /// that, so screens can show a colleague's name instead of their directory object id (see
    /// PeopleEndpoints). Written at most every ten minutes per person, not on every page load.
    /// An admin-set role or status is never overwritten here: the membership store, not this
    /// directory, decides what someone may do. A failure is swallowed on purpose, because signing
    /// in must never fail on account of a display-name write.
    /// </summary>
    private static async Task RecordSignInAsync(
        IUserAccountRepository? repository,
        TenantScope scope,
        string userId,
        string? displayName,
        string? email,
        PlatformRole role,
        CancellationToken cancellationToken)
    {
        if (repository is null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        try
        {
            var existing = await repository.GetAsync(userId, scope, cancellationToken);
            var name = string.IsNullOrWhiteSpace(displayName) || displayName == userId ? null : displayName.Trim();
            var now = DateTimeOffset.UtcNow;

            if (existing is null)
            {
                if (name is null)
                {
                    return;
                }

                await repository.SaveAsync(new InsuranceUserAccount
                {
                    UserId = userId,
                    Scope = scope,
                    Name = name,
                    Email = email ?? string.Empty,
                    Role = role,
                    EntraObjectId = userId,
                    LastLogin = now,
                }, cancellationToken);
                return;
            }

            var nameChanged = name is not null && name != existing.Name;
            var stale = existing.LastLogin is null || now - existing.LastLogin > TimeSpan.FromMinutes(10);
            if (!nameChanged && !stale)
            {
                return;
            }

            await repository.SaveAsync(existing with
            {
                Name = name ?? existing.Name,
                Email = string.IsNullOrWhiteSpace(email) ? existing.Email : email,
                EntraObjectId = existing.EntraObjectId ?? userId,
                LastLogin = now,
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Deliberately ignored; see the summary above.
        }
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
            PlatformRole.Admin => "Workspace Administrator",
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
