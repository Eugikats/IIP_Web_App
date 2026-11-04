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
    /// <summary>Model version string returned by the API response (e.g. "gpt-4o-2024-08-06"). Null when not available.</summary>
    public string? ModelVersion { get; init; }
    /// <summary>Prompt tokens consumed. Null when provider does not return usage.</summary>
    public int? PromptTokens { get; init; }
    /// <summary>Completion tokens consumed. Null when provider does not return usage.</summary>
    public int? CompletionTokens { get; init; }
    /// <summary>Estimated cost in USD based on known list prices. Null when not calculable.</summary>
    public decimal? EstimatedCostUsd { get; init; }

    /// <summary><see cref="ActionLogKinds.Action"/> for something an agent or assistant did; <see cref="ActionLogKinds.ModelCall"/>
    /// for the bare record of one call to a model, written only to meter usage. Model-call records carry no prompt and no
    /// answer, and never appear in the lists people read. Absent on rows stored before metering, which are actions.</summary>
    public string RecordKind { get; init; } = ActionLogKinds.Action;
}

public static class ActionLogKinds
{
    public const string Action = "Action";
    public const string ModelCall = "ModelCall";
}
