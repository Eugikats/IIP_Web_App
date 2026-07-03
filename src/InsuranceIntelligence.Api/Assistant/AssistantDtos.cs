namespace InsuranceIntelligence.Api.Assistant;

using InsuranceIntelligence.Api.Common;

public sealed record PolicyQuestionRequest(
    Guid? CaseId,
    string? Question,
    string? ProductLine,
    string? Audience);

public sealed record PolicyQuestionResponse(
    string Answer,
    decimal ConfidenceScore,
    bool EscalationRequired,
    string GuardrailMessage,
    string RiskFlag,
    string CoverageState,
    IReadOnlyCollection<SourceCitationResponse> Sources,
    Guid AgentActionLogId,
    string ModelProvider,
    string? ModelDeployment,
    string PromptVersion,
    string Visibility,
    bool CanTriggerDecision,
    DateTimeOffset GeneratedAt,
    string CorrelationId);

public sealed record SourceCitationResponse(
    Guid KnowledgeDocumentId,
    string ChunkId,
    string DocumentName,
    string ProductLine,
    string Version,
    string Excerpt,
    string Label);

public sealed record AgentActionLogResponse(
    Guid AgentActionLogId,
    Guid? CaseId,
    string AgentName,
    string AssistantMode,
    string InputSummary,
    IReadOnlyCollection<SourceCitationResponse> RetrievedSources,
    string ModelOutput,
    decimal ConfidenceScore,
    string RiskFlag,
    bool HumanOverride,
    DateTimeOffset CreatedAt,
    string CorrelationId,
    string ModelProvider,
    string? ModelDeployment,
    string PromptVersion,
    string Visibility,
    bool CanTriggerDecision);
