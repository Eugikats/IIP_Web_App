using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Notifications;

public enum TeamsNotificationType
{
    ApprovalRequired,
    SlaAtRisk,
    SlaBreached,
    CustomerUpdatePending,
    ClaimEscalated,
    DocumentReviewNeeded,
    ReviewRequested,
    ClaimAssigned,

    /// <summary>Outcome P: "capture interest as a lead in the existing list and route it to a
    /// licensed person." A lead has no assigned officer at creation (nothing decided that yet),
    /// so routing is a broadcast to the sales channel, the same shape ApprovalRequired already
    /// uses for a queue with no single named recipient.</summary>
    LeadCreated
}

public sealed record TeamsNotificationRequest(
    TenantScope Scope,
    Guid? CaseId,
    TeamsNotificationType NotificationType,
    string Message,
    string RequestedByUserId,
    string CorrelationId);

public interface ITeamsNotificationProvider
{
    Task<string> SendAsync(TeamsNotification notification, CancellationToken cancellationToken = default);
}
