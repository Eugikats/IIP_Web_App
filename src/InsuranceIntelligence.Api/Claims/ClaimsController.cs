using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Claims;

public static class ClaimsController
{
    public static async Task<IResult> ListClaims(
        string? search,
        string? status,
        string? riskLevel,
        string? channel,
        string? assignedUserId,
        bool? attentionOnly,
        int? skip,
        int? take,
        IClaimsQueryService claimsQueryService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TrySetTenantContext(httpContext, tenantContextSetter, options, out var error))
        {
            return error;
        }

        try
        {
            var query = new ClaimsQuery(search, status, riskLevel, channel, assignedUserId, attentionOnly, skip ?? 0, take ?? 50);
            return Results.Ok(await claimsQueryService.ListAsync(query, cancellationToken));
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> GetClaim(
        Guid claimId,
        IClaimsQueryService claimsQueryService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TrySetTenantContext(httpContext, tenantContextSetter, options, out var error))
        {
            return error;
        }

        try
        {
            var claim = await claimsQueryService.GetAsync(claimId, cancellationToken);
            return claim is null
                ? Results.NotFound(new { message = "Claim was not found for the requested tenant workspace." })
                : Results.Ok(claim);
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> GetAttention(
        IClaimsQueryService claimsQueryService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TrySetTenantContext(httpContext, tenantContextSetter, options, out var error))
        {
            return error;
        }

        try
        {
            return Results.Ok(await claimsQueryService.GetAttentionAsync(cancellationToken));
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> GetDailySummary(
        string? date,
        IClaimsQueryService claimsQueryService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TrySetTenantContext(httpContext, tenantContextSetter, options, out var error))
        {
            return error;
        }

        if (!string.IsNullOrWhiteSpace(date)
            && !DateOnly.TryParse(date, out _))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["date"] = ["Date must be a valid ISO date."]
            });
        }

        var summaryDate = string.IsNullOrWhiteSpace(date)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.Parse(date);

        try
        {
            return Results.Ok(await claimsQueryService.GetDailySummaryAsync(summaryDate, cancellationToken));
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> GetDraftContext(
        Guid claimId,
        IClaimsQueryService claimsQueryService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TrySetTenantContext(httpContext, tenantContextSetter, options, out var error))
        {
            return error;
        }

        try
        {
            var draftContext = await claimsQueryService.GetDraftContextAsync(claimId, cancellationToken);
            return draftContext is null
                ? Results.NotFound(new { message = "Claim was not found for the requested tenant workspace." })
                : Results.Ok(draftContext);
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
        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            error = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
            return false;
        }

        var role = DemoAuthorization.GetEffectiveRole(httpContext, options).ToString();
        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            error = Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Authenticated user context is required."]
            });
            return false;
        }

        tenantContextSetter.SetCurrent(new TenantContext(scope.TenantId, scope.WorkspaceId, userId, [role]));
        error = Results.Empty;
        return true;
    }
}
