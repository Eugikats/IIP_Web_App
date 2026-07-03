using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

public sealed record CustomerUpdateDraft
{
    public required Guid UpdateId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required CustomerUpdateReasonCategory ReasonCategory { get; init; }
    public required CommunicationChannel Channel { get; init; }
    public required CustomerUpdateStatus Status { get; init; }
    public required string MessageBody { get; init; }
    public required bool RequiresApproval { get; init; }
    public required bool Sensitive { get; init; }
    public string? DeliveryStatus { get; init; }
    public required string CreatedByUserId { get; init; }
    public string? ApprovedByUserId { get; init; }
    public string? SentByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public required string CorrelationId { get; init; }
}
