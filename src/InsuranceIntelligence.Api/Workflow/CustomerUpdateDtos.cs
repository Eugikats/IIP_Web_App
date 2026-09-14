using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Workflow;

using InsuranceIntelligence.Api.Common;

public sealed record DraftCustomerUpdateRequest(
    string? ReasonCategory,
    string? Channel);

public sealed record CustomerUpdateDecisionRequest(string? Recipient = null);

// WO-16: lets Communications.tsx show a tenant-wide, actionable customer-update queue instead of
// a per-case-only view, each row carries just enough case/customer context to act on it (approve/
// send) without a second round trip per row.
public sealed record CustomerUpdateWithCaseResponse(
    CustomerUpdateResponse Update,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string PolicyNumber);

public sealed record CustomerUpdateResponse(
    Guid UpdateId,
    Guid CaseId,
    string ReasonCategory,
    string Channel,
    string Status,
    string MessageBody,
    bool RequiresApproval,
    bool Sensitive,
    string? DeliveryStatus,
    string CreatedByUserId,
    string? ApprovedByUserId,
    string? SentByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? SentAt,
    string CorrelationId,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);
