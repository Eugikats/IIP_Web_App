using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.CustomerAssistant;

public enum CustomerFollowUpStatus
{
    PendingReview,
    Scheduled,
    Processing,
    Sent,
    Cancelled,
    NeedsHuman,
    Failed,
    Unknown,
}

public enum CustomerFollowUpMode
{
    Review,
    Autopilot,
}

/// <summary>A durable, customer-requested future contact. It contains routing metadata and a
/// reference to an approved template, never a generated free-form message or customer evidence.</summary>
public sealed record CustomerFollowUpTask
{
    public required Guid TaskId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string CustomerId { get; init; }
    public required string Channel { get; init; }
    public required string AccountId { get; init; }
    public required string RecipientId { get; init; }
    public required string ThreadId { get; init; }
    public required string OriginatingMessageId { get; init; }
    public required string Purpose { get; init; }
    public required string TemplateKey { get; init; }
    public required DateTimeOffset DueAtUtc { get; init; }
    public required string TimeZoneId { get; init; }
    public required int PolicyVersion { get; init; }
    public required CustomerFollowUpMode ApprovalMode { get; init; }
    public required CustomerFollowUpStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public string? ProviderMessageId { get; init; }
    public int AttemptCount { get; init; }
    public string? SafeFailureReason { get; init; }
    public required string CorrelationId { get; init; }
}

public interface ICustomerFollowUpRepository
{
    Task SaveAsync(CustomerFollowUpTask task, CancellationToken cancellationToken = default);
    Task<CustomerFollowUpTask?> GetAsync(TenantScope scope, Guid taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerFollowUpTask>> ListAsync(
        TenantScope scope, string? customerId = null, int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerFollowUpTask>> TryClaimDueAsync(
        TenantScope scope, DateTimeOffset now, int limit = 50, CancellationToken cancellationToken = default);
    Task<CustomerFollowUpTask?> TryCancelAsync(
        TenantScope scope, Guid taskId, DateTimeOffset now, string reason, CancellationToken cancellationToken = default);
    Task<int> CancelPendingForRecipientAsync(
        TenantScope scope, string channel, string accountId, string recipientId, DateTimeOffset now,
        string reason, CancellationToken cancellationToken = default);
}
