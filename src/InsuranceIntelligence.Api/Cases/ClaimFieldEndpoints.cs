using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Cases;

public sealed record SaveFieldValuesRequest(Dictionary<string, string>? Values);

/// <summary>
/// The questions a product asks about a claim and the claim's answers (build-loop item 5).
/// Staff routes are scoped to the caller's tenant workspace and to claims the caller may see; the
/// claimant routes are gated by the magic-link session, show only customer-visible fields, and
/// only ever touch the claimant's own claim.
/// </summary>
public static class ClaimFieldEndpoints
{
    public static async Task<IResult> GetForm(
        string? productLine,
        string? claimType,
        IClaimFieldService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(await service.GetFormAsync(scope, productLine, claimType, customerAudience: false, cancellationToken));
    }

    public static async Task<IResult> GetFormForClaimant(
        string? productLine,
        string? claimType,
        IClaimFieldService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(await service.GetFormAsync(scope, productLine, claimType, customerAudience: true, cancellationToken));
    }

    public static async Task<IResult> GetForClaim(
        Guid caseId,
        IClaimFieldService service,
        ICaseRepository caseRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        return claimCase is null
            ? WorkflowEndpoints.NotFoundCase()
            : Results.Ok(await service.GetForClaimAsync(claimCase, customerAudience: false, cancellationToken));
    }

    public static async Task<IResult> SaveForClaim(
        Guid caseId,
        SaveFieldValuesRequest request,
        IClaimFieldService service,
        ICaseRepository caseRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to change a claim.");
        }

        var saved = await service.SaveValuesAsync(
            claimCase, request.Values ?? new Dictionary<string, string>(), customerAudience: false, userId, httpContext.TraceIdentifier, cancellationToken);
        return saved.IsValid
            ? Results.Ok(await service.GetForClaimAsync(saved.Claim!, customerAudience: false, cancellationToken))
            : FieldErrors(saved.Errors);
    }

    public static async Task<IResult> GetForClaimByClaimant(
        Guid caseId,
        IClaimFieldService service,
        ICaseRepository caseRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Validation("tenantScope", error);
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !ClientPortalEndpoints.BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        return Results.Ok(await service.GetForClaimAsync(claimCase, customerAudience: true, cancellationToken));
    }

    public static async Task<IResult> SaveForClaimant(
        Guid caseId,
        SaveFieldValuesRequest request,
        IClaimFieldService service,
        ICaseRepository caseRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Validation("tenantScope", error);
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !ClientPortalEndpoints.BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        var saved = await service.SaveValuesAsync(
            claimCase, request.Values ?? new Dictionary<string, string>(), customerAudience: true, ClientPortalEndpoints.ResolveActor(httpContext), httpContext.TraceIdentifier, cancellationToken);
        return saved.IsValid
            ? Results.Ok(await service.GetForClaimAsync(saved.Claim!, customerAudience: true, cancellationToken))
            : FieldErrors(saved.Errors);
    }

    internal static IResult FieldErrors(IReadOnlyDictionary<string, string> errors) =>
        Results.ValidationProblem(errors.ToDictionary(pair => $"fieldValues.{pair.Key}", pair => new[] { pair.Value }));

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
