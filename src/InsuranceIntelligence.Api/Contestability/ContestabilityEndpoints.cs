using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.Api.Workflow;

namespace InsuranceIntelligence.Api.Contestability;

/// <summary>
/// Staff contestability surface: list / acknowledge / resolve claimant re-review requests.
/// Creating a request never decides a claim; acknowledge/resolve only record human workflow state.
/// </summary>
public static class ContestabilityEndpoints
{
    public static async Task<IResult> ListReviewRequests(
        Guid caseId,
        ICaseRepository caseRepository,
        IClaimReviewRequestRepository reviewRequestRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var requests = await reviewRequestRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(requests
            .OrderByDescending(request => request.RequestedAt)
            .Select(ToResponse)
            .ToArray());
    }

    public static async Task<IResult> AcknowledgeReviewRequest(
        Guid caseId,
        Guid requestId,
        ICaseRepository caseRepository,
        IClaimReviewRequestRepository reviewRequestRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var existing = await reviewRequestRepository.GetAsync(requestId, scope, cancellationToken);
        if (existing is null || existing.CaseId != caseId)
        {
            return Results.NotFound(new { message = "Re-review request was not found for the requested tenant workspace." });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, "claims-officer");
        var correlationId = httpContext.TraceIdentifier;
        var acknowledged = existing.Acknowledge();
        await reviewRequestRepository.SaveAsync(acknowledged, cancellationToken);
        if (acknowledged.Status != existing.Status)
        {
            await auditRepository.WriteAsync(
                AuditEvent.ClaimReviewAcknowledged(acknowledged, actor, correlationId),
                cancellationToken);
        }

        return Results.Ok(ToResponse(acknowledged));
    }

    public static async Task<IResult> ResolveReviewRequest(
        Guid caseId,
        Guid requestId,
        StaffResolveReviewRequestBody body,
        ICaseRepository caseRepository,
        IClaimReviewRequestRepository reviewRequestRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var existing = await reviewRequestRepository.GetAsync(requestId, scope, cancellationToken);
        if (existing is null || existing.CaseId != caseId)
        {
            return Results.NotFound(new { message = "Re-review request was not found for the requested tenant workspace." });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, "claims-officer");
        var correlationId = httpContext.TraceIdentifier;
        var resolved = existing.Resolve(actor, body?.ResolutionNote);
        await reviewRequestRepository.SaveAsync(resolved, cancellationToken);
        if (resolved.Status != existing.Status || resolved.ResolvedAt != existing.ResolvedAt)
        {
            await auditRepository.WriteAsync(
                AuditEvent.ClaimReviewResolved(resolved, actor, correlationId),
                cancellationToken);
        }

        return Results.Ok(ToResponse(resolved));
    }

    private static StaffReviewRequestResponse ToResponse(ClaimReviewRequest request) =>
        new(
            request.RequestId,
            request.Status.ToString(),
            request.Origin.ToString(),
            request.RequestedByUserId,
            request.Reason,
            request.RequestedAt,
            request.ResolvedAt,
            request.ResolvedByUserId,
            request.ResolutionNote);
}

public sealed record StaffResolveReviewRequestBody(string? ResolutionNote);

public sealed record StaffReviewRequestResponse(
    Guid RequestId,
    string Status,
    string Origin,
    string RequestedByUserId,
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByUserId,
    string? ResolutionNote);
