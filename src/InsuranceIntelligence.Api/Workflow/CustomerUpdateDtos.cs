using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Workflow;

using InsuranceIntelligence.Api.Common;

public sealed record DraftCustomerUpdateRequest(
    string? ReasonCategory,
    string? Channel);

public sealed record CustomerUpdateDecisionRequest(string? Recipient = null);

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
