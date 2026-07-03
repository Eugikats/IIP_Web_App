using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

public sealed record ApprovalRequest
{
    public required Guid ApprovalId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required string ApprovalType { get; init; }
    public required ApprovalStatus Status { get; init; }
    public required string RiskLevel { get; init; }
    public required string EvidenceSummary { get; init; }
    public required IReadOnlyCollection<string> SourceReferences { get; init; }
    public required string RequestedByUserId { get; init; }
    public string? ReviewerUserId { get; init; }
    public ApprovalDecision? Decision { get; init; }
    public string? Comments { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public required string CorrelationId { get; init; }
}
