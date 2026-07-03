using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Auth;

public static class RuntimeHeaderGuard
{
    private const string GraphWebhookPath = "/api/integrations/email/graph/webhook";

    private static readonly string[] DevelopmentOnlyHeaders =
    [
        "x-demo-role",
        "X-Demo-Role",
        "x-demo-user-id",
        "X-User-Id",
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

        if (DevelopmentOnlyHeaders.Any(header => httpContext.Request.Headers.ContainsKey(header))
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

        if (!httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || httpContext.Request.Path.StartsWithSegments(GraphWebhookPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return httpContext.User.Identity?.IsAuthenticated == true
            ? null
            : Results.Unauthorized();
    }
}
