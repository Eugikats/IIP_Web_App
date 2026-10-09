using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.CommandCenter;

/// <summary>
/// Chat-first Command Center query endpoint: <c>POST /api/v1/command-center/query</c>.
/// Returns a governed, tenant-scoped set of typed answer blocks assembled by a deterministic
/// planner from real data. Preparation only, the envelope's <c>canTriggerDecision</c> is always
/// <c>false</c> and every proposed action requires human approval.
/// </summary>
public static class CommandCenterQueryEndpoints
{
    public static async Task<IResult> RunQuery(
        CommandCenterQueryRequest request,
        ICommandCenterConversationService conversationService,
        ITenantContextSetter tenantContextSetter,
        InsuranceIntelligence.Application.Core.IPermissionService permissionService,
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
            return Results.Ok(await conversationService.QueryAsync(request, GetCorrelationId(httpContext), cancellationToken));
        }
        catch (InsuranceIntelligence.Application.Billing.AiCreditsExhaustedException)
        {
            // The question needed the model and the month's credits are used up. Say so as an answer, not an error.
            var canAdd = await permissionService.HasPermissionAsync(InsuranceIntelligence.Domain.Administration.Permission.ViewBilling, cancellationToken);
            return Results.Ok(new CommandCenterQueryResponse(
                [new CreditsNoticeBlock(
                    canAdd
                        ? "AI credits for this month are used up, so this question cannot be answered now. Questions that only count your records still work. You can add credits in Settings, under Usage and billing."
                        : "AI credits for this month are used up, so this question cannot be answered now. Questions that only count your records still work. Ask your administrator to add credits.",
                    canAdd)],
                Guid.Empty, GetCorrelationId(httpContext), "None", "credits-notice-v1", "Internal", CanTriggerDecision: false, DateTimeOffset.UtcNow));
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
        catch (CommandCenterConversationNotFoundException)
        {
            return Results.NotFound();
        }
        catch (CommandCenterConversationConflictException)
        {
            return Results.Conflict(new { code = "conversation_conflict", message = "The conversation changed or cannot be continued in its current state." });
        }
    }

    internal static bool TrySetTenantContext(
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

    internal static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}
