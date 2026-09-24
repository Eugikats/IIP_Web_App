using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Retention;

/// <summary>Outcome O: "reminder drafts prepared for approval, never auto sent, consent gated."
/// A renewal-outreach message about a policy nearing its cover end, distinct from
/// CustomerUpdateDraft (Outcome D) because it is not about any claim: CustomerUpdateDraft.CaseId
/// is required throughout that system, and a policy up for renewal frequently has no open claim
/// at all. Same spirit (draft, human approval, then send), separate entity so this stays additive
/// rather than widening an already-depended-upon type.</summary>
public enum RetentionReminderStatus
{
    PendingApproval,
    Approved,
    Sent,
    ConsentRequired,
    ConfigurationRequired,
    Failed
}

public sealed record RetentionReminderDraft
{
    public required Guid ReminderId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string PolicyId { get; init; }
    public required string PolicyNumber { get; init; }
    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }

    /// <summary>Which expiry window this reminder was drafted for: 60, 30 or 14.</summary>
    public required int ExpiryBucketDays { get; init; }

    public required CommunicationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string MessageBody { get; init; }
    public required RetentionReminderStatus Status { get; init; }
    public required string CreatedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public string? ApprovedByUserId { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public string? DeliveryStatus { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public required string CorrelationId { get; init; }
}

public interface IRetentionReminderRepository
{
    Task SaveAsync(RetentionReminderDraft reminder, CancellationToken cancellationToken = default);

    Task<RetentionReminderDraft?> GetAsync(Guid reminderId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RetentionReminderDraft>> ListForPolicyAsync(string policyId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RetentionReminderDraft>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
