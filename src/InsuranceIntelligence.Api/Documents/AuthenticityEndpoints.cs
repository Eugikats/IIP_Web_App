using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Documents;

public sealed record AuthenticityDispositionRequest(string? Disposition, string? Note);

/// <summary>
/// Authenticity checks on a claim's pictures and a person's reading of them (build-loop item 16).
/// Staff only, in the caller's workspace, with the permission to work a claim. The checks are advice;
/// the disposition is a human's, needs a note unless it is "no concern", and is audited.
/// </summary>
public static class AuthenticityEndpoints
{
    public static async Task<IResult> List(
        Guid caseId,
        ICaseRepository caseRepository,
        IAuthenticityService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return Results.NotFound(new { message = "Claim was not found." });
        }

        return Results.Ok(await service.ListAsync(scope, caseId, cancellationToken));
    }

    public static async Task<IResult> SetDisposition(
        Guid caseId,
        Guid documentId,
        AuthenticityDispositionRequest request,
        IAuthenticityService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var result = await service.SetDispositionAsync(scope, caseId, documentId, request?.Disposition, request?.Note, userId, httpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            AuthenticityOutcome.Done => Results.Ok(result.Report),
            AuthenticityOutcome.NotFound => Results.NotFound(new { message = result.Message }),
            _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["disposition"] = [result.Message ?? "That is not a valid reading."] }),
        };
    }
}
