using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.ClientPortal;

/// <summary>
/// Resolves tenant scope for claimant-facing endpoints from plain X-Tenant-Id/X-Workspace-Id
/// headers, regardless of AuthMode. Unlike <see cref="InsuranceIntelligence.Api.Common.TenantScopeReader"/>
/// (used for staff routes), this never requires an authenticated Entra principal, a claimant
/// reaching the portal through a magic link is never an Entra tenant member and so can never carry
/// the claims TenantScopeReader requires outside Development.
///
/// This is safe: knowing a tenant/workspace id alone grants no access to any case. Every
/// claimant-facing route beyond magic-link issuance is gated by a verified
/// <see cref="InsuranceIntelligence.Domain.ClientPortal.ClaimantPortalSession"/>
/// (see <see cref="ClaimantSessionEndpointFilter"/>), and issuance itself never discloses whether
/// a case exists in the requested tenant/workspace.
/// </summary>
public static class ClientPortalScopeReader
{
    public static bool TryGetFromHeaders(HttpContext httpContext, out TenantScope scope, out string validationError)
    {
        var tenantId = ReadHeader(httpContext, "X-Tenant-Id");
        var workspaceId = ReadHeader(httpContext, "X-Workspace-Id");

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
