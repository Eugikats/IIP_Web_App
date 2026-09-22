using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Notifications;

/// <summary>
/// A user-facing, in-app notification (WO-14), distinct from <see cref="TeamsNotification"/>,
/// which is an OUTBOUND delivery to Microsoft Teams. This is generated only from real events that
/// already occur elsewhere in the system (an approval assignment, a claim escalation, an SLA
/// breach, a document extraction needing review, a review request) and read by the recipient in
/// the portal. Never synthesised, never a sample/demo row (AGENTS.md A5).
/// </summary>
public sealed record Notification(
    Guid NotificationId,
    TenantScope Scope,
    string RecipientUserId,
    NotificationType Type,
    string Message,
    Guid CaseId,
    DateTimeOffset CreatedAt,
    bool IsRead,
    DateTimeOffset? ReadAt);

public enum NotificationType
{
    ApprovalAssigned,
    ClaimEscalated,
    SlaBreach,
    DocumentReviewNeeded,
    ReviewRequested,
    ClaimAssigned,
}

public interface INotificationRepository
{
    Task SaveAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<Notification?> GetAsync(Guid notificationId, TenantScope scope, string recipientUserId, CancellationToken cancellationToken = default);

    /// <summary>Most recent first. Scoped to both tenant/workspace AND the recipient, never
    /// another user's notifications, even within the same tenant (I5).</summary>
    Task<IReadOnlyCollection<Notification>> ListForUserAsync(
        TenantScope scope, string recipientUserId, int limit, CancellationToken cancellationToken = default);

    Task<int> CountUnreadForUserAsync(TenantScope scope, string recipientUserId, CancellationToken cancellationToken = default);

    /// <summary>Used only to de-duplicate notification types generated from a recomputed-on-every-
    /// read condition (SLA breach) rather than a one-shot event, so re-viewing a breached claim
    /// doesn't create a new notification each time.</summary>
    Task<bool> ExistsAsync(
        TenantScope scope, string recipientUserId, NotificationType type, Guid caseId, CancellationToken cancellationToken = default);
}
