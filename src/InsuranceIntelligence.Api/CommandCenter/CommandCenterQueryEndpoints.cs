using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.CommandCenter;

/// <summary>
/// Chat-first Command Center query endpoint: <c>POST /api/v1/command-center/query</c>.
/// Returns a governed, tenant-scoped set of typed answer blocks assembled by a deterministic
/// planner from real data. Preparation only — the envelope's <c>canTriggerDecision</c> is always
/// <c>false</c> and every proposed action requires human approval.
/// </summary>
public static class CommandCenterQueryEndpoints
{
    public static async Task<IResult> RunQuery(
        CommandCenterQueryRequest request,
        ICommandCenterQueryService queryService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TrySetTenantContext(httpContext, tenantContextSetter, options, out var tenantContextError))
        {
            return tenantContextError;
        }

        try
        {
            return Results.Ok(await queryService.QueryAsync(request, GetCorrelationId(httpContext), cancellationToken));
        }
        catch (CommandCenterValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.Field] = [ex.Message],
            });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    private static bool TrySetTenantContext(
        HttpContext httpContext,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        out IResult error)
    {
        if (tenantContextSetter is ITenantContextAccessor { HasCurrent: true })
        {
            error = Results.Empty;
            return true;
        }

        // Deployed (Entra) mode: defer to the middleware-resolved store context (ADR-0005); never
        // re-derive scope from the token tid/headers. Fail closed if no membership was resolved.
        if (!options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            error = Results.StatusCode(StatusCodes.Status403Forbidden);
            return false;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            error = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError],
            });
            return false;
        }

        var role = DemoAuthorization.GetEffectiveRole(httpContext, options).ToString();
        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            error = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Authenticated user context is required."],
            });
            return false;
        }

        tenantContextSetter.SetCurrent(new TenantContext(scope.TenantId, scope.WorkspaceId, userId, [role]));
        error = Results.Empty;
        return true;
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}
