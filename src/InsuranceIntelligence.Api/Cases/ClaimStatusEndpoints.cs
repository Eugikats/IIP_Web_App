using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Cases;

namespace InsuranceIntelligence.Api.Cases;

public sealed record ChangeClaimStatusRequest(string? ToStatus, string? Reason);

/// <summary>
/// The one "Change status" action on a claim (build-loop item 20): what the caller may choose from the
/// claim's current status, and the change itself with a required reason. Anyone who can work a claim may
/// use it for the moves an officer decides; the service holds the rule for each move, including the moves
/// that need a supervisor. Settling, closing, escalating and approving keep their own actions.
/// </summary>
public static class ClaimStatusEndpoints
{
    public static async Task<IResult> Options(
        Guid caseId,
        IClaimStatusChangeService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var options = await service.OptionsAsync(scope, caseId, cancellationToken);
        return options is null ? Results.NotFound(new { message = "Claim was not found." }) : Results.Ok(options);
    }

    public static async Task<IResult> Change(
        Guid caseId,
        ChangeClaimStatusRequest request,
        IClaimStatusChangeService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Authenticated user context is required to change a claim's status."] });
        }

        var result = await service.ChangeAsync(scope, caseId, request?.ToStatus, request?.Reason, userId, httpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            StatusChangeOutcome.Done => Results.Ok(new { caseId = result.CaseId, status = result.Status, label = result.Label }),
            StatusChangeOutcome.NotFound => Results.NotFound(new { message = result.Message }),
            StatusChangeOutcome.NeedsSupervisor => Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status403Forbidden),
            StatusChangeOutcome.HumanRequired => Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status403Forbidden),
            StatusChangeOutcome.NotAllowed => Results.Conflict(new { message = result.Message }),
            _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [result.Message ?? "That is not a valid change."] }),
        };
    }
}
