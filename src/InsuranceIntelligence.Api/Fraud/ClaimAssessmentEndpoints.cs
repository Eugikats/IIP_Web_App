using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Fraud;

namespace InsuranceIntelligence.Api.Fraud;

public sealed record AssessmentResponseRequest(string? Response, string? Note);

/// <summary>
/// The review score and recommendation for a claim, and a person's response to it (build-loop item 18).
/// Limited to the roles that already see fraud signals, in the caller's workspace. Everything here is
/// advice: reading it changes nothing, and the response is a person's, audited, and needs a reason
/// when it disagrees.
/// </summary>
public static class ClaimAssessmentEndpoints
{
    public static async Task<IResult> Get(
        Guid caseId,
        IClaimAssessmentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var assessment = await service.GetAsync(scope, caseId, cancellationToken);
        return assessment is null ? Results.NotFound(new { message = "Claim was not found." }) : Results.Ok(assessment);
    }

    public static async Task<IResult> Refresh(
        Guid caseId,
        IClaimAssessmentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var assessment = await service.PrepareAsync(scope, caseId, userId, httpContext.TraceIdentifier, cancellationToken);
        return assessment is null ? Results.NotFound(new { message = "Claim was not found." }) : Results.Ok(assessment);
    }

    public static async Task<IResult> Respond(
        Guid caseId,
        AssessmentResponseRequest request,
        IClaimAssessmentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var result = await service.RespondAsync(scope, caseId, request?.Response, request?.Note, userId, httpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            AssessmentOutcome.Done => Results.Ok(result.Assessment),
            AssessmentOutcome.NotFound => Results.NotFound(new { message = result.Message }),
            _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["response"] = [result.Message ?? "That is not a valid response."] }),
        };
    }
}
