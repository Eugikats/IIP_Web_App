using System.Text.Json.Serialization;

namespace InsuranceIntelligence.Application.MyWork;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MyWorkSourceType
{
    WorkflowTask,
    Approval,
    DocumentReview,
    CommunicationReview,
    AgentWorkItem
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MyWorkOwnership
{
    Mine,
    Unassigned,
    All
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MyWorkState
{
    Ready,
    InProgress,
    Escalated,
    Completed,
    Closed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MyWorkDueState
{
    Overdue,
    DueSoon,
    OnTrack,
    NoDueDate
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MyWorkEvidenceReadiness
{
    Ready,
    Gaps,
    NotAssessed
}

public sealed record MyWorkQuery(
    MyWorkOwnership Ownership,
    MyWorkSourceType? SourceType,
    MyWorkState? State,
    int Limit);

public sealed record MyWorkItemDto(
    string Id,
    MyWorkSourceType SourceType,
    string SourceId,
    string? CaseId,
    string Title,
    string RequiredAction,
    string SourceStatus,
    MyWorkState State,
    string? OwnerUserId,
    string? OwnerTeam,
    string OwnerBasis,
    string Priority,
    DateTimeOffset? DueAt,
    MyWorkDueState DueState,
    MyWorkEvidenceReadiness EvidenceReadiness,
    bool ApprovalRequired,
    string Href,
    DateTimeOffset UpdatedAt);

public sealed record MyWorkSummary(
    int Total,
    int Overdue,
    int Unassigned,
    int ApprovalRequired);

public sealed record MyWorkResponse(
    MyWorkOwnership Ownership,
    int ReturnedCount,
    bool HasMore,
    IReadOnlyList<MyWorkItemDto> Items,
    MyWorkSummary Summary);

public interface IMyWorkService
{
    Task<MyWorkResponse> ListAsync(MyWorkQuery query, CancellationToken cancellationToken = default);
}
