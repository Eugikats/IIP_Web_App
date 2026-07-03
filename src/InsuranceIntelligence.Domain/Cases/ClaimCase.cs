using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

public sealed record ClaimCase
{
    public required Guid CaseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }
    public required string PolicyNumber { get; init; }
    public required string ProductLine { get; init; }
    public required string ClaimType { get; init; }
    public required string Channel { get; init; }
    public required DateOnly IncidentDate { get; init; }
    public required string IncidentDescription { get; init; }
    public required CaseStatus Status { get; init; }
    public required decimal CompletenessScore { get; init; }
    public string RiskLevel { get; init; } = "Low";
    public CasePriority Priority { get; init; } = CasePriority.Normal;
    public DateTimeOffset? SlaDueAt { get; init; }
    public string? AssignedUserId { get; init; }
    public string? AssignedTeam { get; init; }
    public required string CreatedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CorrelationId { get; init; }

    public ClaimCaseSnapshot ToAuditSnapshot() =>
        new(CaseId, Scope, Status.ToString());
}
