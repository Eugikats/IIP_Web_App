using System.Security.Claims;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Auth;

public static class UiContextEndpoints
{
    public static IResult GetMe(HttpContext httpContext, InfrastructureOptions options)
    {
        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tenantScope"] = [validationError]
                });
            }

            var role = DemoAuthorization.GetRole(httpContext);
            var userId = DemoAuthorization.GetUserId(httpContext, UserIdForRole(role));
            return Results.Ok(BuildResponse(
                userId,
                DisplayNameForRole(role),
                EmailForUser(userId),
                scope,
                [role],
                options,
                source: "Development"));
        }

        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return Results.Problem(
                "Microsoft Entra authentication is required before backend-owned UI context can be issued.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        var roles = ReadRoles(httpContext.User);
        var scopeResult = ReadAuthenticatedScope(httpContext.User);
        if (scopeResult.ValidationError is { Length: > 0 })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [scopeResult.ValidationError]
            });
        }

        return Results.Ok(BuildResponse(
            ReadClaim(httpContext.User, ClaimTypes.NameIdentifier, "oid", "sub") ?? "authenticated-user",
            ReadClaim(httpContext.User, "name", ClaimTypes.Name) ?? "Authenticated user",
            ReadClaim(httpContext.User, ClaimTypes.Email, "preferred_username", "upn") ?? "user@example.invalid",
            scopeResult.Scope,
            roles.Count == 0 ? [PlatformRole.Client] : roles,
            options,
            source: "Entra"));
    }

    private static UiContextResponse BuildResponse(
        string userId,
        string displayName,
        string email,
        TenantScope scope,
        IReadOnlyCollection<PlatformRole> roles,
        InfrastructureOptions options,
        string source)
    {
        var permissions = roles
            .SelectMany(RolePermissionMap.ForRole)
            .Distinct()
            .Select(permission => permission.ToString())
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new UiContextResponse(
            userId,
            displayName,
            email,
            scope.TenantId,
            TenantName(scope.TenantId),
            scope.WorkspaceId,
            WorkspaceName(scope.WorkspaceId),
            roles.Select(role => role.ToString()).ToArray(),
            ["Motor"],
            permissions,
            AuthorityFor(roles),
            new UiFeatureFlags(
                AiConversationsEnabled: permissions.Contains(Permission.ViewAiOutputs.ToString(), StringComparer.Ordinal),
                PolicyIntelligenceEnabled: permissions.Contains(Permission.ViewAiOutputs.ToString(), StringComparer.Ordinal),
                DocumentIntelligenceEnabled: permissions.Contains(Permission.UploadDocument.ToString(), StringComparer.Ordinal),
                IntegrationsEnabled: permissions.Contains(Permission.ConfigureIntegrations.ToString(), StringComparer.Ordinal),
                CustomerPortalEnabled: false),
            new UiDataScope(
                Claims: permissions.Contains(Permission.ViewAllClaims.ToString(), StringComparer.Ordinal) ? "team" : "assigned",
                Policies: "business_line",
                Audit: permissions.Contains(Permission.ViewAuditLog.ToString(), StringComparer.Ordinal) ? "read_only_if_authorised" : "unavailable"),
            new UiEnvironmentContext("Development".Equals(options.AuthMode, StringComparison.OrdinalIgnoreCase) ? "Local Development" : "Authenticated Runtime", RuntimeMode(options), source));
    }

    private static UiAuthority AuthorityFor(IReadOnlyCollection<PlatformRole> roles)
    {
        var canReview = roles.Any(role => role is PlatformRole.ClaimsSupervisor or PlatformRole.Compliance);
        var canOverride = roles.Any(role => role is PlatformRole.ClaimsSupervisor or PlatformRole.Compliance);

        return new UiAuthority(
            Currency: "UGX",
            ReviewApprovalLimit: canReview ? 5_000_000m : 0m,
            RequiresEscalationAbove: canReview ? 5_000_000m : 0m,
            CanOverrideAiRecommendation: canOverride,
            CanApprovePayout: false);
    }

    private static IReadOnlyCollection<PlatformRole> ReadRoles(ClaimsPrincipal user)
    {
        return user.Claims
            .Where(claim => claim.Type is ClaimTypes.Role or "roles" or "role")
            .SelectMany(claim => claim.Value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => Enum.TryParse<PlatformRole>(value, true, out var role) ? role : (PlatformRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .Distinct()
            .ToArray();
    }

    private static (TenantScope Scope, string ValidationError) ReadAuthenticatedScope(ClaimsPrincipal user)
    {
        var tenantId = ReadClaim(user, "tid", "tenant_id", "tenantId");
        var workspaceId = ReadClaim(user, "workspace_id", "workspaceId", "iip_workspace");

        try
        {
            return (TenantScope.Create(tenantId ?? string.Empty, workspaceId ?? string.Empty), string.Empty);
        }
        catch (ArgumentException ex)
        {
            return (new TenantScope(string.Empty, string.Empty), ex.Message);
        }
    }

    private static string? ReadClaim(ClaimsPrincipal user, params string[] names)
    {
        foreach (var name in names)
        {
            var value = user.FindFirst(name)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string RuntimeMode(InfrastructureOptions options) =>
        options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "LocalTest" : "Authenticated";

    private static string UserIdForRole(PlatformRole role) => role switch
    {
        PlatformRole.ClaimsSupervisor => "local-supervisor",
        PlatformRole.CustomerService => "local-service-agent",
        PlatformRole.ITAdmin => "local-it-admin",
        PlatformRole.Compliance => "local-compliance",
        PlatformRole.KnowledgeManager => "local-knowledge-manager",
        PlatformRole.Client => "local-client",
        _ => "local-admin"
    };

    private static string DisplayNameForRole(PlatformRole role) => role switch
    {
        PlatformRole.ClaimsSupervisor => "Jane Namusoke",
        PlatformRole.ClaimsOfficer => "Claims Officer",
        PlatformRole.CustomerService => "Service Agent",
        PlatformRole.ITAdmin => "IT Administrator",
        PlatformRole.Compliance => "Compliance Officer",
        PlatformRole.KnowledgeManager => "Knowledge Manager",
        PlatformRole.Client => "Policyholder",
        _ => "Local Administrator"
    };

    private static string EmailForUser(string userId) => $"{userId}@example.invalid";

    private static string TenantName(string tenantId) =>
        tenantId.Equals("tenant-demo", StringComparison.OrdinalIgnoreCase) || tenantId.Equals("tenant-visual-fixture", StringComparison.OrdinalIgnoreCase)
            ? "Uganda Insurer Ltd."
            : tenantId;

    private static string WorkspaceName(string workspaceId) =>
        workspaceId.Equals("workspace-claims", StringComparison.OrdinalIgnoreCase) || workspaceId.Equals("workspace-visual-fixture", StringComparison.OrdinalIgnoreCase)
            ? "Kampala Motor Claims"
            : workspaceId;
}

public sealed record UiContextResponse(
    string UserId,
    string DisplayName,
    string Email,
    string TenantId,
    string TenantName,
    string WorkspaceId,
    string WorkspaceName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> BusinessLines,
    IReadOnlyCollection<string> Permissions,
    UiAuthority Authority,
    UiFeatureFlags UiFeatures,
    UiDataScope DataScope,
    UiEnvironmentContext Environment);

public sealed record UiAuthority(
    string Currency,
    decimal ReviewApprovalLimit,
    decimal RequiresEscalationAbove,
    bool CanOverrideAiRecommendation,
    bool CanApprovePayout);

public sealed record UiFeatureFlags(
    bool AiConversationsEnabled,
    bool PolicyIntelligenceEnabled,
    bool DocumentIntelligenceEnabled,
    bool IntegrationsEnabled,
    bool CustomerPortalEnabled);

public sealed record UiDataScope(
    string Claims,
    string Policies,
    string Audit);

public sealed record UiEnvironmentContext(
    string Name,
    string Mode,
    string Source);
