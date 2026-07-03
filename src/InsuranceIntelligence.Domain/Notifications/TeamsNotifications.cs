using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Notifications;

public enum TeamsNotificationType
{
    ApprovalRequired,
    SlaAtRisk,
    SlaBreached,
    CustomerUpdatePending
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
