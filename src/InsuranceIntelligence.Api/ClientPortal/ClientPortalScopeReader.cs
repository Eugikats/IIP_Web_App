using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.ClientPortal;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

/// <summary>
/// Resolves tenant scope for claimant-facing endpoints. A claimant reaching the portal through a
/// magic link is never an Entra tenant member, so this can never rely on the claims
/// <see cref="InsuranceIntelligence.Api.Common.TenantScopeReader"/> requires for staff routes.
///
/// Which insurer's portal this is comes from, in order (build-loop item 29):
/// 1. The short name in the customer's link, sent by the page as X-Portal-Slug, looked up in the portal directory.
/// 2. The workspace's own verified web address, taken from the browser's Origin.
/// 3. Outside Development on a deployment dedicated to one insurer: that insurer, pinned by configuration.
/// 4. In Development: the X-Tenant-Id and X-Workspace-Id headers as sent.
/// Nothing a browser sends can name a workspace on its own outside Development: a slug or address must be one the platform
/// knows, and where the deployment is pinned to one insurer a different workspace is refused. Knowing a workspace's name grants
/// no access to any case anyway: every claimant route past link issuance needs a verified
/// <see cref="InsuranceIntelligence.Domain.ClientPortal.ClaimantPortalSession"/> (see <see cref="ClaimantSessionEndpointFilter"/>),
/// and issuance never discloses whether a case exists.
/// </summary>
public static class ClientPortalScopeReader
{
    public const string SlugHeader = "X-Portal-Slug";

    public static bool TryGetFromHeaders(HttpContext httpContext, out TenantScope scope, out string validationError)
    {
        var tenantId = ReadHeader(httpContext, "X-Tenant-Id");
        var workspaceId = ReadHeader(httpContext, "X-Workspace-Id");
        var slug = ReadHeader(httpContext, SlugHeader);

        var options = httpContext.RequestServices?.GetService(typeof(InfrastructureOptions)) as InfrastructureOptions;
        var directory = httpContext.RequestServices?.GetService(typeof(IPortalDirectory)) as IPortalDirectory;
        var development = options is null || options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
        var pinned = options is not null
            && !development
            && !string.IsNullOrWhiteSpace(options.TenantId)
            && !string.IsNullOrWhiteSpace(options.WorkspaceId);

        // 1 and 2: the customer's link, or the workspace's own address.
        TenantScope? named = null;
        if (slug is not null)
        {
            var bySlug = directory?.BySlug(slug) ?? directory?.FindBySlugAsync(slug).GetAwaiter().GetResult();
            if (bySlug is null)
            {
                return Refuse("This claim link is not valid. Use the link you were sent.", out scope, out validationError);
            }

            named = bySlug.Scope;
        }
        else if (OriginHost(httpContext) is { } host && directory is not null)
        {
            var byHost = directory.ByHostname(host) ?? directory.FindByHostnameAsync(host).GetAwaiter().GetResult();
            named = byHost?.Scope;
        }

        if (named is { } workspace)
        {
            var disagrees = (tenantId is not null && !string.Equals(tenantId, workspace.TenantId, StringComparison.OrdinalIgnoreCase))
                || (workspaceId is not null && !string.Equals(workspaceId, workspace.WorkspaceId, StringComparison.OrdinalIgnoreCase));
            var otherThanPinned = pinned
                && (!string.Equals(workspace.TenantId, options!.TenantId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(workspace.WorkspaceId, options.WorkspaceId, StringComparison.OrdinalIgnoreCase));
            if ((disagrees && !development) || otherThanPinned)
            {
                return Refuse("This claim portal belongs to a different insurer.", out scope, out validationError);
            }

            scope = workspace;
            validationError = string.Empty;
            return true;
        }

        // 3: a deployment dedicated to one insurer.
        if (pinned)
        {
            var namesAnotherTenant = tenantId is not null && !string.Equals(tenantId, options!.TenantId, StringComparison.OrdinalIgnoreCase);
            var namesAnotherWorkspace = workspaceId is not null && !string.Equals(workspaceId, options!.WorkspaceId, StringComparison.OrdinalIgnoreCase);
            if (namesAnotherTenant || namesAnotherWorkspace)
            {
                return Refuse("This claim portal belongs to a different insurer.", out scope, out validationError);
            }

            tenantId = options!.TenantId;
            workspaceId = options.WorkspaceId;
        }
        else if (!development)
        {
            // A shared deployment with no link or address that names an insurer cannot guess one, and the browser's own
            // headers are not allowed to choose.
            return Refuse("This claim portal does not say which insurer it belongs to. Use the link you were sent.", out scope, out validationError);
        }

        // 4 (and the pinned case): the scope as configured or, in Development, as sent.
        try
        {
            scope = TenantScope.Create(tenantId ?? string.Empty, workspaceId ?? string.Empty);
            validationError = string.Empty;
            return true;
        }
        catch (ArgumentException ex)
        {
            scope = new TenantScope(string.Empty, string.Empty);
            validationError = ex.Message;
            return false;
        }
    }

    private static bool Refuse(string error, out TenantScope scope, out string validationError)
    {
        scope = new TenantScope(string.Empty, string.Empty);
        validationError = error;
        return false;
    }

    private static string? OriginHost(HttpContext httpContext)
    {
        var origin = ReadHeader(httpContext, "Origin");
        return origin is not null && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.Host : null;
    }

    private static string? ReadHeader(HttpContext httpContext, string headerName) =>
        httpContext.Request.Headers.TryGetValue(headerName, out var header) && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : null;
}
