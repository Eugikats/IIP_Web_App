using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Compliance;

/// <summary>Manual/administrative surface for retention (blueprint §14 Phase 6, WO 6.3) —
/// EnforceRetention is what a real scheduled job (see RetentionEnforcementTimerFunction) calls on
/// a timer; exposing it here too means an officer or the demo run doesn't have to wait for the
/// clock, and the manual and scheduled paths are provably the same code, not two implementations
/// that can drift.</summary>
public static class RetentionEndpoints
{
    public static async Task<IResult> ListPolicies(
        IRetentionPolicyRepository policyRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out var tenantContext))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        var configured = await policyRepository.ListAsync(tenantContext.Scope, cancellationToken);
        var configuredByClass = configured.ToDictionary(policy => policy.DataClass);

        var response = Enum.GetValues<RetentionDataClass>()
            .Select(dataClass => configuredByClass.TryGetValue(dataClass, out var policy)
                ? new RetentionPolicyResponse(dataClass.ToString(), policy.RetentionDays, true, policy.UpdatedByUserId, policy.UpdatedAt)
                : new RetentionPolicyResponse(dataClass.ToString(), RetentionPolicy.DefaultRetentionDays(dataClass), false, null, null))
            .ToArray();

        return Results.Ok(response);
    }

    public static async Task<IResult> SavePolicy(
        SaveRetentionPolicyRequest request,
        IRetentionPolicyRepository policyRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out var tenantContext))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        if (!Enum.TryParse<RetentionDataClass>(request.DataClass, true, out var dataClass))
        {
            return Validation("dataClass", "Unrecognized retention data class.");
        }

        if (request.RetentionDays < 1)
        {
            return Validation("retentionDays", "Retention days must be at least 1.");
        }

        var policy = new RetentionPolicy
        {
            Scope = tenantContext.Scope,
            DataClass = dataClass,
            RetentionDays = request.RetentionDays,
            UpdatedByUserId = tenantContext.UserId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await policyRepository.SaveAsync(policy, cancellationToken);

        return Results.Ok(new RetentionPolicyResponse(dataClass.ToString(), policy.RetentionDays, true, policy.UpdatedByUserId, policy.UpdatedAt));
    }

    public static async Task<IResult> EnforceRetention(
        IRetentionEnforcementService enforcementService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out var tenantContext))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);
        var results = await enforcementService.EnforceAsync(tenantContext.Scope, correlationId, cancellationToken);

        return Results.Ok(results.Select(result => new RetentionEnforcementResultResponse(
            result.DataClass.ToString(),
            result.RetentionDaysApplied,
            result.RecordsDeleted,
            result.OldestDeletedOccurredAt,
            result.NewestDeletedOccurredAt,
            result.RunAt)).ToArray());
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static bool TryRequireTenantContext(HttpContext httpContext, out TenantContext tenantContext)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } accessor)
        {
            tenantContext = accessor.Current;
            return true;
        }

        tenantContext = null!;
        return false;
    }
}

public sealed record RetentionPolicyResponse(
    string DataClass,
    int RetentionDays,
    bool ExplicitlyConfigured,
    string? UpdatedByUserId,
    DateTimeOffset? UpdatedAt);

public sealed record SaveRetentionPolicyRequest(string DataClass, int RetentionDays);

public sealed record RetentionEnforcementResultResponse(
    string DataClass,
    int RetentionDaysApplied,
    int RecordsDeleted,
    DateTimeOffset? OldestDeletedOccurredAt,
    DateTimeOffset? NewestDeletedOccurredAt,
    DateTimeOffset RunAt);
