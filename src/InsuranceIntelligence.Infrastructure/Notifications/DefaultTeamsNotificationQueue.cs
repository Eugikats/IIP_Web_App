using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public class DefaultTeamsNotificationQueue(
    IMessageQueueDispatcher dispatcher,
    ITeamsNotificationRepository repository,
    InfrastructureOptions options) : ITeamsNotificationQueue
{
    public async Task EnqueueAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
    {
        await repository.SaveAsync(notification, cancellationToken);

        var envelope = new QueueMessageEnvelope(
            QueueName: options.ServiceBus.ApprovalNotificationsQueue,
            MessageType: "TeamsNotification.Dispatch",
            TenantId: notification.Scope.TenantId,
            WorkspaceId: notification.Scope.WorkspaceId,
            CorrelationId: Guid.NewGuid().ToString(),
            Payload: new { 
                NotificationId = notification.Id,
                CaseId = notification.CaseId,
                NotificationType = notification.NotificationType.ToString(),
                PayloadJson = notification.PayloadJson
            }
        );

        await dispatcher.DispatchAsync(envelope, cancellationToken);
    }
}
