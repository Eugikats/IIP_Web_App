using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Workflow;

public sealed record CreateApprovalRequest(
    string? ApprovalType,
    string? EvidenceSummary,
    IReadOnlyCollection<string>? SourceReferences,
    string? RiskLevel);

public sealed record ApprovalDecisionRequest(string? Comments);

public sealed record ApprovalResponse(
    Guid ApprovalId,
    Guid CaseId,
    string ApprovalType,
    string Status,
    string RiskLevel,
    string EvidenceSummary,
    IReadOnlyCollection<string> SourceReferences,
    string RequestedByUserId,
    string? ReviewerUserId,
    string? Decision,
    string? Comments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DecidedAt,
    string CorrelationId,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);
