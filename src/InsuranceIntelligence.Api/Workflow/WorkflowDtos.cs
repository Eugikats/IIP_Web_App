namespace InsuranceIntelligence.Api.Workflow;

public sealed record RecommendedActionResponse(
    string ActionType,
    string Label,
    string Summary,
    bool RequiresHumanApproval,
    string Priority,
    IReadOnlyCollection<string> Evidence);

public sealed record WorkflowEvaluationResponse(
    Guid CaseId,
    string SlaStatus,
    IReadOnlyCollection<RecommendedActionResponse> RecommendedActions,
    IReadOnlyCollection<WorkflowTaskResponse> CreatedTasks,
    IReadOnlyCollection<ApprovalResponse> CreatedApprovals);

public sealed record WorkflowTaskResponse(
    Guid TaskId,
    Guid CaseId,
    string TaskType,
    string Status,
    string Title,
    string Summary,
    string? AssignedUserId,
    string? AssignedTeam,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    string CorrelationId);

public sealed record AssignWorkflowTaskRequest(string? AssignedUserId, string? AssignedTeam);

public sealed record CompleteWorkflowTaskRequest(string? Comments);

public sealed record CaseTimelineEventResponse(
    Guid EventId,
    Guid CaseId,
    string EventType,
    string Actor,
    DateTimeOffset Timestamp,
    string Summary,
    string? BeforeStatus,
    string? AfterStatus,
    string CorrelationId);
