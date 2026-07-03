using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

public sealed record WorkflowTask
{
    public required Guid TaskId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required WorkflowTaskType TaskType { get; init; }
    public required WorkflowTaskStatus Status { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public string? AssignedUserId { get; init; }
    public string? AssignedTeam { get; init; }
    public required string CreatedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public required string CorrelationId { get; init; }
}
