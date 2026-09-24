using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Workflow;

using InsuranceIntelligence.Api.Common;

public sealed record DraftCustomerUpdateRequest(
    string? ReasonCategory,
    string? Channel);

public sealed record CustomerUpdateDecisionRequest(string? Recipient = null);

public sealed record EditCustomerUpdateRequest(
    string? MessageBody,
    string? ShortMessageBody,
    bool ApproveInSameStep = false);

public sealed record WriteCustomerUpdateRequest(
    string? ReasonCategory,
    string? Channel,
    string? MessageBody,
    string? ShortMessageBody);

public sealed record CreateCustomerUpdateFromTemplateRequest(
    string? TemplateKey,
    string? Channel);

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
    string ShortMessageBody,
    string Language,
    string CreationMethod,
    string? OriginalMessageBody,
    string? EditedByUserId,
    DateTimeOffset? EditedAt,
    string? SourceTemplateKey,
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
