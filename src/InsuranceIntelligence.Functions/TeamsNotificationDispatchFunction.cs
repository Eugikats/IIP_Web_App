using System.Text.Json;
using InsuranceIntelligence.Infrastructure.Notifications;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Queue;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

public class TeamsNotificationDispatchFunction(
    ITeamsNotificationProvider provider,
    ITeamsNotificationRepository repository,
    ILogger<TeamsNotificationDispatchFunction> logger)
{
    public const string ApprovalNotificationsQueueSetting = "%ServiceBus:ApprovalNotificationsQueue%";
    public const string ServiceBusConnectionSetting = "ServiceBusConnection";

    [Function("TeamsNotificationDispatch")]
    public async Task Run(
        [ServiceBusTrigger(ApprovalNotificationsQueueSetting, Connection = ServiceBusConnectionSetting)] string queueMessage,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing Teams notification dispatch message.");
        TeamsNotification? notification = null;

        try
        {
            var envelope = JsonSerializer.Deserialize<QueueMessageEnvelope>(queueMessage);
            if (envelope is null
                || envelope.Payload is null
                || string.IsNullOrWhiteSpace(envelope.TenantId)
                || string.IsNullOrWhiteSpace(envelope.WorkspaceId)
                || string.IsNullOrWhiteSpace(envelope.CorrelationId)
                || !envelope.MessageType.Equals("TeamsNotification.Dispatch", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Teams notification queue message is invalid.");
            }

            if (envelope.Payload is not JsonElement payloadElement || payloadElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Teams notification queue payload is invalid.");
            }

            var notificationIdValue = payloadElement.TryGetProperty("NotificationId", out var notificationIdProperty)
                && notificationIdProperty.ValueKind == JsonValueKind.String
                    ? notificationIdProperty.GetString()
                    : null;
            if (!Guid.TryParse(notificationIdValue, out var notificationId))
            {
                throw new InvalidDataException("Teams notification ID is invalid.");
            }

            var notificationTypeValue = payloadElement.TryGetProperty("NotificationType", out var notificationTypeProperty)
                && notificationTypeProperty.ValueKind == JsonValueKind.String
                    ? notificationTypeProperty.GetString()
                    : null;
            if (!Enum.TryParse<TeamsNotificationType>(notificationTypeValue, true, out var notificationType))
            {
                throw new InvalidDataException("Teams notification type is invalid.");
            }

            var caseIdValue = payloadElement.TryGetProperty("CaseId", out var caseIdProperty)
                && caseIdProperty.ValueKind == JsonValueKind.String
                    ? caseIdProperty.GetString()
                    : null;
            var payloadJson = payloadElement.TryGetProperty("PayloadJson", out var payloadProperty)
                && payloadProperty.ValueKind == JsonValueKind.String
                    ? payloadProperty.GetString()
                    : null;

            notification = new TeamsNotification
            {
                Id = notificationId,
                Scope = new Domain.Common.TenantScope(envelope.TenantId, envelope.WorkspaceId),
                CaseId = Guid.TryParse(caseIdValue, out var caseId) ? caseId : null,
                NotificationType = notificationType,
                PayloadJson = payloadJson ?? "{}"
            };

            var deliveryStatus = await provider.SendAsync(notification, cancellationToken);
            if (!deliveryStatus.StartsWith("delivered_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Teams notification provider did not confirm delivery.");
            }

            notification.DeliveryStatus = TeamsNotificationDeliveryStatus.Delivered;
            notification.DeliveredAt = DateTimeOffset.UtcNow;
            notification.ErrorMessage = null;
            await repository.SaveAsync(notification, cancellationToken);
            logger.LogInformation("Teams notification delivered successfully with status {Status}.", deliveryStatus);
        }
        catch (Exception ex)
        {
            if (notification is not null && ex is not OperationCanceledException)
            {
                notification.DeliveryStatus = TeamsNotificationDeliveryStatus.Failed;
                notification.ErrorMessage = "Delivery failed. Review correlated provider telemetry.";
                await repository.SaveAsync(notification, CancellationToken.None);
            }

            logger.LogError(ex, "Error dispatching Teams notification.");
            throw;
        }
    }
}
