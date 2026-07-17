using InsuranceIntelligence.Domain.Common;
using System.Text.Json.Serialization;

namespace InsuranceIntelligence.Domain.AgentWorkflows;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentWorkflowDefinitionStatus
{
    Draft,
    Published,
    Archived
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentWorkflowNodeType
{
    Trigger,
    Agent,
    Tool,
    Decision,
    HumanReview,
    Complete
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentWorkflowRunStatus
{
    Queued,
    Running,
    WaitingForHuman,
    Completed,
    Failed,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentWorkflowNodeRunStatus
{
    Pending,
    Queued,
    Running,
    WaitingForHuman,
    Completed,
    Failed,
    Skipped
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentWorkItemStatus
{
    RequiresReview,
    InProgress,
    Completed,
    ReturnedForCorrection,
    Escalated,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentWorkItemDecision
{
    AcceptPreparedWork,
    ReturnForCorrection,
    Escalate,
    CancelWorkflow
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentWorkflowSideEffect
{
    ReadOnly,
    DraftOnly,
    InternalWrite,
    ApprovalRequired,
    ExternalSend
}

public sealed record AgentWorkflowPosition(decimal X, decimal Y);

public sealed record AgentWorkflowNode(
    string Key,
    string Name,
    string Description,
    AgentWorkflowNodeType Type,
    string? AgentKey,
    string? ToolKey,
    AgentWorkflowSideEffect SideEffect,
    AgentWorkflowPosition Position,
    bool RequiresHumanReview,
    IReadOnlyDictionary<string, string> Configuration);

public sealed record AgentWorkflowEdge(
    string SourceNodeKey,
    string TargetNodeKey,
    string? Outcome,
    string? Label);

public sealed record AgentWorkflowDefinition(
    Guid DefinitionId,
    TenantScope? Scope,
    string Key,
    string Name,
    string Description,
    int Release,
    int Version,
    AgentWorkflowDefinitionStatus Status,
    bool IsSystemDefinition,
    IReadOnlyList<string> BusinessReferenceTypes,
    IReadOnlyList<AgentWorkflowNode> Nodes,
    IReadOnlyList<AgentWorkflowEdge> Edges,
    DateTimeOffset CreatedAt,
    string CreatedByUserId,
    DateTimeOffset? PublishedAt,
    string? PublishedByUserId);

/// <summary>
/// A safe pointer to grounding evidence. It deliberately stores references and
/// optional bounded display metadata, not raw OCR, prompt, email, claim, or policy text.
/// </summary>
public sealed record AgentEvidenceReference(
    string SourceType,
    string SourceId,
    string? DisplayLabel,
    int? PageNumber,
    IReadOnlyList<decimal>? BoundingPolygon,
    decimal? Confidence,
    string? ContentHash);

public sealed record AgentWorkflowNodeRun(
    string NodeKey,
    AgentWorkflowNodeRunStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Outcome,
    string? OutputSummary,
    IReadOnlyList<AgentEvidenceReference> Evidence,
    string? Provider,
    string? ModelOrTool,
    string? PromptVersion,
    string? ErrorCode,
    int AttemptCount);

public sealed record AgentWorkflowRun(
    Guid RunId,
    TenantScope Scope,
    Guid DefinitionId,
    string WorkflowKey,
    int WorkflowVersion,
    string BusinessReferenceType,
    string BusinessReferenceId,
    AgentWorkflowRunStatus Status,
    IReadOnlyList<AgentWorkflowNodeRun> NodeRuns,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    string StartedByUserId,
    string CorrelationId,
    string? FailureCode);

public sealed record AgentWorkItem(
    Guid WorkItemId,
    TenantScope Scope,
    Guid RunId,
    string WorkflowKey,
    string NodeKey,
    string Title,
    string ReviewInstructions,
    string BusinessReferenceType,
    string BusinessReferenceId,
    AgentWorkItemStatus Status,
    IReadOnlyList<AgentEvidenceReference> Evidence,
    string? AssignedToUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DecidedAt,
    string? DecidedByUserId,
    AgentWorkItemDecision? Decision,
    string? DecisionReason,
    string CorrelationId);
