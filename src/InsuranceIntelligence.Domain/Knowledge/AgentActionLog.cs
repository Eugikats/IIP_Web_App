using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record AgentActionLog
{
    public required Guid AgentActionLogId { get; init; }
    public required TenantScope Scope { get; init; }
    public Guid? CaseId { get; init; }
    public required string UserId { get; init; }
    public required string AgentName { get; init; }
    public required AssistantMode AssistantMode { get; init; }
    public required string InputSummary { get; init; }
    public required IReadOnlyCollection<SourceCitation> RetrievedSources { get; init; }
    public required string ModelOutput { get; init; }
    public required AiConfidenceScore ConfidenceScore { get; init; }
    public required string RiskFlag { get; init; }
    public required bool HumanOverride { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CorrelationId { get; init; }
    public string ModelProvider { get; init; } = "Unknown";
    public string? ModelDeployment { get; init; }
    public string PromptVersion { get; init; } = "unversioned";
    public string Visibility { get; init; } = "Internal";
    public bool CanTriggerDecision { get; init; }
}
