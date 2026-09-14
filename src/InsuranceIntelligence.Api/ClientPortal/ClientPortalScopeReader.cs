using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

/// <summary>
/// Resolves tenant scope for claimant-facing endpoints. A claimant reaching the portal through a
/// magic link is never an Entra tenant member, so this can never rely on the claims
/// <see cref="InsuranceIntelligence.Api.Common.TenantScopeReader"/> requires for staff routes.
///
/// Outside Development the scope is pinned to this deployment's own configured tenant and
/// workspace. The browser may still send X-Tenant-Id and X-Workspace-Id, but they are only
/// accepted when they name this deployment; a request naming any other insurer is refused. That is
/// what makes it safe for <see cref="InsuranceIntelligence.Api.Auth.RuntimeHeaderGuard"/> to let
/// those two headers through on client-portal routes: they can confirm the scope, never choose it.
/// Before this, the guard rejected the headers outright while this reader required them, so the
/// claimant portal could not issue a single link under Entra auth.
///
/// In Development, where there is no pinned deployment scope, the headers are used as sent.
/// Knowing a tenant id alone still grants no access to any case: every claimant route past link
/// issuance needs a verified <see cref="InsuranceIntelligence.Domain.ClientPortal.ClaimantPortalSession"/>
/// (see <see cref="ClaimantSessionEndpointFilter"/>), and issuance never discloses whether a case exists.
/// </summary>
public static class ClientPortalScopeReader
{
    public static bool TryGetFromHeaders(HttpContext httpContext, out TenantScope scope, out string validationError)
    {
        var tenantId = ReadHeader(httpContext, "X-Tenant-Id");
        var workspaceId = ReadHeader(httpContext, "X-Workspace-Id");

        var options = httpContext.RequestServices?.GetService(typeof(InfrastructureOptions)) as InfrastructureOptions;
        var pinned = options is not null
            && !options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.TenantId)
            && !string.IsNullOrWhiteSpace(options.WorkspaceId);

        if (pinned)
        {
            var namesAnotherTenant = tenantId is not null && !string.Equals(tenantId, options!.TenantId, StringComparison.OrdinalIgnoreCase);
            var namesAnotherWorkspace = workspaceId is not null && !string.Equals(workspaceId, options!.WorkspaceId, StringComparison.OrdinalIgnoreCase);
            if (namesAnotherTenant || namesAnotherWorkspace)
            {
                scope = new TenantScope(string.Empty, string.Empty);
                validationError = "This claim portal belongs to a different insurer.";
                return false;
            }

            tenantId = options!.TenantId;
            workspaceId = options.WorkspaceId;
        }

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

    private static string? ReadHeader(HttpContext httpContext, string headerName) =>
        httpContext.Request.Headers.TryGetValue(headerName, out var header) && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : null;
}
