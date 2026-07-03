namespace InsuranceIntelligence.Domain.Notifications;

public interface ITeamsNotificationQueue
{
    Task EnqueueAsync(TeamsNotification notification, CancellationToken cancellationToken = default);
}
