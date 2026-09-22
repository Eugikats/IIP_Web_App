using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Cases;

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
    MotorFnolDetails? MotorFnolDetails,
    MedicalFnolDetails? MedicalFnolDetails,
    string Status,
    decimal CompletenessScore,
    string RiskLevel,
    string Priority,
    DateTimeOffset? SlaDueAt,
    bool RequiresConfirmation,
    string? AssignedUserId,
    string? AssignedTeam,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CorrelationId,
    SafeAuditSummary Audit,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions,
    RequiredDocumentsResponse? RequiredDocuments = null,
    decimal? ClaimedAmount = null,
    IReadOnlyCollection<string>? AdvisoryRiskSignals = null);

/// <summary>The document-completeness check at the moment this response was built, WO 5.3: tell
/// the submitter what is missing at submission, not only after extraction runs later. Null when
/// the caller did not resolve a checklist (e.g. actions unrelated to intake); a real checklist
/// with an empty Missing collection is a genuine "nothing outstanding," not the same as null.</summary>
public sealed record RequiredDocumentsResponse(
    IReadOnlyCollection<string> Required,
    IReadOnlyCollection<string> Optional,
    IReadOnlyCollection<string> Missing);

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
    bool RequiresConfirmation,
    string? AssignedUserId,
    string? AssignedTeam,
    DateTimeOffset UpdatedAt,
    SafeAuditSummary Audit,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);

public sealed record AssignCaseRequest(
    string? AssignedUserId,
    string? AssignedTeam,
    string? AuditReason);

public sealed record EscalateCaseRequest(
    string AuditReason,
    string? AssignedTeam);

public sealed record ConfirmCaseRequest(
    string AuditReason,
    string? ProductLine = null,
    string? ClaimType = null,
    DateOnly? IncidentDate = null,
    string? IncidentDescription = null);
