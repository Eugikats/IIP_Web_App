using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.ClaimsSummary;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Claims;

/// <summary>WO-10: exposes the claims-summary agent. Gate check lives here (not in the service),
/// matching the established pattern in AssistantEndpoints/CustomerUpdateEndpoints.</summary>
public static class ClaimsSummaryEndpoints
{
    public static async Task<IResult> SummarizeCase(
        Guid caseId,
        ICaseRepository caseRepository,
        IClaimsSummaryService summaryService,
        IAgentGate agentGate,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);

        var gate = await agentGate.CheckAsync("claims-summary", cancellationToken);
        if (!gate.Enabled)
        {
            return Results.Json(
                new { status = "AgentDisabled", agentKey = "claims-summary", message = gate.RefusalMessage },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        ClaimsSummaryResult result;
        try
        {
            result = await summaryService.SummarizeAsync(caseId, scope, actor, correlationId, cancellationToken);
        }
        catch (CaseNotFoundException)
        {
            // Defensive: WorkflowEndpoints.GetScopedCaseAsync above already checked existence and
            // scope with the same permission, so this should not occur in practice — kept as a
            // safety net rather than letting an internal-consistency gap surface as a raw 500.
            return WorkflowEndpoints.NotFoundCase();
        }

        return Results.Ok(new ClaimsSummaryResponse(
            result.CaseId,
            result.Summary,
            result.OpenQuestions,
            result.KnowledgeSources.Select(ToCitationResponse).ToArray(),
            result.EvidenceRefs,
            result.Confidence,
            result.AgentActionLogId,
            result.CorrelationId));
    }

    private static SourceCitationResponse ToCitationResponse(SourceCitation citation) =>
        new(citation.Label, citation.DocumentName, citation.ChunkId, citation.Excerpt);
}

public sealed record ClaimsSummaryResponse(
    Guid CaseId,
    string Summary,
    IReadOnlyCollection<string> OpenQuestions,
    IReadOnlyCollection<SourceCitationResponse> KnowledgeSources,
    IReadOnlyCollection<string> EvidenceRefs,
    decimal Confidence,
    Guid AgentActionLogId,
    string CorrelationId);

public sealed record SourceCitationResponse(string Label, string DocumentName, string ChunkId, string Excerpt);
