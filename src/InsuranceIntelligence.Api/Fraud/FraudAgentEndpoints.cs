using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Fraud;

namespace InsuranceIntelligence.Api.Fraud;

/// <summary>
/// The fraud agent's review of a claim. Limited to the roles that already see fraud signals, in the
/// caller's workspace. Reading it calls no model; running it does, and is audited. Everything here is
/// advice for a person.
/// </summary>
public static class FraudAgentEndpoints
{
    public static async Task<IResult> Get(
        Guid caseId,
        IFraudAgentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var review = await service.GetAsync(scope, caseId, cancellationToken);
        return review is null ? Results.NotFound(new { message = "Claim was not found." }) : Results.Ok(review);
    }

    public static async Task<IResult> Run(
        Guid caseId,
        IFraudAgentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var result = await service.RunAsync(scope, caseId, userId, httpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            FraudAgentOutcome.Done => Results.Ok(result.Review),
            FraudAgentOutcome.NotFound => Results.NotFound(new { message = result.Message }),
            FraudAgentOutcome.Disabled => Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }
}
