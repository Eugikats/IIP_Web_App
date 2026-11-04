using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Cases;

public sealed record AcceptFieldProposalRequest(string? Value, bool ReplaceConfirmed = false);

/// <summary>
/// Values read from a claim's documents, offered for its questions (build-loop item 15). Officers
/// see the suggestions with their source document, page and confidence and accept (optionally
/// correcting), or reject them. Everything is scoped to the caller's own workspace and needs the
/// permission to work a claim; nothing is applied without a person pressing accept.
/// </summary>
public static class FieldProposalEndpoints
{
    public static async Task<IResult> List(
        Guid caseId,
        ICaseRepository caseRepository,
        IFieldProposalService service,
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

    public static async Task<IResult> Accept(
        Guid caseId,
        Guid proposalId,
        AcceptFieldProposalRequest? request,
        IFieldProposalService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var result = await service.AcceptAsync(scope, caseId, proposalId, request?.Value, request?.ReplaceConfirmed ?? false, userId, httpContext.TraceIdentifier, cancellationToken);
        return ToHttp(result);
    }

    public static async Task<IResult> Reject(
        Guid caseId,
        Guid proposalId,
        IFieldProposalService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        return ToHttp(await service.RejectAsync(scope, caseId, proposalId, userId, httpContext.TraceIdentifier, cancellationToken));
    }

    private static IResult ToHttp(FieldProposalResult result) =>
        result.Outcome switch
        {
            FieldProposalOutcome.Done => Results.Ok(result.Proposal),
            FieldProposalOutcome.NotFound => Results.NotFound(new { message = result.Message }),
            FieldProposalOutcome.NotPending => Results.Conflict(new { message = result.Message, code = "already_decided" }),
            FieldProposalOutcome.NeedsReplace => Results.Conflict(new { message = result.Message, code = "needs_replace" }),
            _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["value"] = [result.Message ?? "That value does not fit the question."] }),
        };
}
