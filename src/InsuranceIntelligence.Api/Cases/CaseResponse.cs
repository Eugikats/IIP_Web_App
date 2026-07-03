using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Cases;

using InsuranceIntelligence.Api.Common;

public sealed record CaseResponse(
    Guid CaseId,
    string TenantId,
    string WorkspaceId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string PolicyNumber,
    string ProductLine,
    string ClaimType,
    string Channel,
    DateOnly IncidentDate,
    string IncidentDescription,
    string Status,
    decimal CompletenessScore,
    string RiskLevel,
    string Priority,
    DateTimeOffset? SlaDueAt,
    string? AssignedUserId,
    string? AssignedTeam,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CorrelationId,
    SafeAuditSummary Audit,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);

public sealed record SafeAuditSummary(
    int EventCount,
    string? LastActionName,
    DateTimeOffset? LastEventAt,
    string? CorrelationId);

public sealed record ClaimInboxItemResponse(
    Guid CaseId,
    string CustomerName,
    string PolicyNumber,
    string ProductLine,
    string ClaimType,
    string Channel,
    string Status,
    decimal CompletenessScore,
    string DocumentStatus,
    string RiskLevel,
    string Priority,
    string SlaStatus,
    string? AssignedUserId,
    string? AssignedTeam,
    DateTimeOffset UpdatedAt,
    SafeAuditSummary Audit,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);
