using System.Text.Json;
using InsuranceIntelligence.Infrastructure.Notifications;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Queue;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

public class TeamsNotificationDispatchFunction(
    ITeamsNotificationProvider provider,
    ILogger<TeamsNotificationDispatchFunction> logger)
{
    [Function("TeamsNotificationDispatch")]
    public async Task Run(
        [ServiceBusTrigger("approval-notifications", Connection = "ServiceBusConnection")] string queueMessage,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing Teams notification dispatch message.");

        try
        {
            var envelope = JsonSerializer.Deserialize<QueueMessageEnvelope>(queueMessage);
            if (envelope == null || envelope.Payload == null)
            {
                logger.LogWarning("Invalid QueueMessageEnvelope format.");
                return;
            }

            var payloadElement = (JsonElement)envelope.Payload;
            var caseIdStr = payloadElement.TryGetProperty("CaseId", out var caseIdProp) && caseIdProp.ValueKind == JsonValueKind.String ? caseIdProp.GetString() : null;
            var typeStr = payloadElement.TryGetProperty("NotificationType", out var typeProp) && typeProp.ValueKind == JsonValueKind.String ? typeProp.GetString() : null;
            var payloadJson = payloadElement.TryGetProperty("PayloadJson", out var jsonProp) && jsonProp.ValueKind == JsonValueKind.String ? jsonProp.GetString() : null;

            var notificationType = Enum.TryParse<TeamsNotificationType>(typeStr, out var parsedType) 
                ? parsedType 
                : TeamsNotificationType.ApprovalRequired;

            var notification = new TeamsNotification
            {
                Scope = new Domain.Common.TenantScope(envelope.TenantId, envelope.WorkspaceId),
                CaseId = Guid.TryParse(caseIdStr, out var id) ? id : null,
                NotificationType = notificationType,
                PayloadJson = payloadJson ?? "{}"
            };

            var deliveryStatus = await provider.SendAsync(notification, cancellationToken);
            
            logger.LogInformation("Teams notification delivered successfully with status {Status}.", deliveryStatus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dispatching Teams notification.");
            throw; // Rethrow to let Service Bus handle retry/deadletter
        }
    }
}
