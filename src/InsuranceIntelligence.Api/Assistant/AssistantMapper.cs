using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Assistant;

public static class AssistantMapper
{
    public static SourceCitationResponse ToResponse(SourceCitation citation) =>
        new(
            citation.KnowledgeDocumentId,
            citation.ChunkId,
            citation.DocumentName,
            citation.ProductLine,
            citation.Version,
            citation.Excerpt,
            citation.Label);

    public static AgentActionLogResponse ToResponse(AgentActionLog actionLog) =>
        new(
            actionLog.AgentActionLogId,
            actionLog.CaseId,
            actionLog.AgentName,
            actionLog.AssistantMode.ToString(),
            actionLog.InputSummary,
            actionLog.RetrievedSources.Select(ToResponse).ToArray(),
            actionLog.ModelOutput,
            actionLog.ConfidenceScore.Value,
            actionLog.RiskFlag,
            actionLog.HumanOverride,
            actionLog.CreatedAt,
            actionLog.CorrelationId,
            actionLog.ModelProvider,
            actionLog.ModelDeployment,
            actionLog.PromptVersion,
            actionLog.Visibility,
            actionLog.CanTriggerDecision,
            ModelVersion: actionLog.ModelVersion,
            PromptTokens: actionLog.PromptTokens,
            CompletionTokens: actionLog.CompletionTokens,
            EstimatedCostUsd: actionLog.EstimatedCostUsd);
}
