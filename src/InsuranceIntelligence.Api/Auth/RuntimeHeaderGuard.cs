using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Auth;

public static class RuntimeHeaderGuard
{
    private const string GraphWebhookPath = "/api/v1/integrations/email/graph/webhook";
    private const string WhatsAppWebhookPath = "/api/v1/integrations/whatsapp/webhook";
    private const string ClientPortalPath = "/api/v1/client-portal";

    // IIP Academy is open to visitors who have not signed in: the course catalogue, certificate checks and "who am I"
    // (which answers a visitor with "signed out"). Everything else under /api/v1/academy still needs a signed-in person.
    private const string AcademyPublicPath = "/api/v1/academy/public";
    private const string AcademyMePath = "/api/v1/academy/me";

    private static readonly string[] IdentityHeaders =
    [
        "x-demo-role",
        "X-Demo-Role",
        "x-demo-user-id",
        "X-User-Id"
    ];

    private static readonly string[] ScopeHeaders =
    [
        "X-Tenant-Id",
        "X-Workspace-Id"
    ];

    private static readonly string[] DevelopmentOnlyQueryParameters =
    [
        "tenantId",
        "workspaceId"
    ];

    public static IResult? RejectBrowserAuthorityOutsideDevelopment(HttpContext httpContext, InfrastructureOptions options)
    {
        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The claimant portal names its insurer with X-Tenant-Id and X-Workspace-Id, because a
        // claimant is never a signed-in tenant member and has no token to carry scope. On those
        // routes the two headers carry no authority: ClientPortalScopeReader pins scope to this
        // deployment's own tenant and workspace outside Development and refuses any other value.
        // Rejecting them here as well left the claimant portal unable to issue a single link under
        // Entra auth. Identity and role headers stay rejected on every route.
        var rejected = IsClientPortal(httpContext)
            ? IdentityHeaders
            : IdentityHeaders.Concat(ScopeHeaders);

        if (rejected.Any(header => httpContext.Request.Headers.ContainsKey(header))
            || DevelopmentOnlyQueryParameters.Any(parameter => httpContext.Request.Query.ContainsKey(parameter)))
        {
            return Results.Problem(
                "Browser-supplied tenant, workspace, user, or demo role context is not accepted outside Development auth mode.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    public static IResult? RejectUnauthenticatedApiOutsideDevelopment(HttpContext httpContext, InfrastructureOptions options)
    {
        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Routes whose callers can never hold an Entra token, each protected by its own mechanism
        // instead: Microsoft Graph and Meta call their webhooks directly, and claimants authenticate
        // with a single-use magic link and the session it grants (ClaimantSessionEndpointFilter).
        // Without these exemptions every WhatsApp message and every claimant request was answered
        // with 401 in any deployed environment.
        if (httpContext.Request.Path.StartsWithSegments(GraphWebhookPath, StringComparison.OrdinalIgnoreCase)
            || httpContext.Request.Path.StartsWithSegments(WhatsAppWebhookPath, StringComparison.OrdinalIgnoreCase)
            || IsClientPortal(httpContext)
            || httpContext.Request.Path.StartsWithSegments(AcademyPublicPath, StringComparison.OrdinalIgnoreCase)
            || httpContext.Request.Path.Equals(AcademyMePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return httpContext.User.Identity?.IsAuthenticated == true
            ? null
            : Results.Unauthorized();
    }

    private static bool IsClientPortal(HttpContext httpContext) =>
        httpContext.Request.Path.StartsWithSegments(ClientPortalPath, StringComparison.OrdinalIgnoreCase);
}
