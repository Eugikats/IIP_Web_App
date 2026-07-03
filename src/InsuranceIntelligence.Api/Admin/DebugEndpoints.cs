using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Admin;

public static class DebugEndpoints
{
    public static async Task<IResult> TriggerTeamsNotification(
        [FromBody] DebugNotificationRequest request,
        IMessageQueueDispatcher dispatcher,
        InfrastructureOptions options,
        CancellationToken cancellationToken)
    {
        var envelope = new QueueMessageEnvelope(
            QueueName: options.ServiceBus.ApprovalNotificationsQueue,
            MessageType: "TeamsNotification.Dispatch",
            TenantId: request.TenantId ?? "tenant-1",
            WorkspaceId: request.WorkspaceId ?? "workspace-1",
            CorrelationId: Guid.NewGuid().ToString(),
            Payload: new
            {
                NotificationId = Guid.NewGuid(),
                CaseId = request.CaseId ?? Guid.NewGuid(),
                NotificationType = "ApprovalRequired",
                PayloadJson = $$"""
                {
                    "BorrowerName": "{{request.CustomerName ?? "Jane Doe"}}",
                    "Amount": {{request.Amount ?? 50000}},
                    "Risk": "{{request.Risk ?? "High"}}"
                }
                """
            }
        );

        await dispatcher.DispatchAsync(envelope, cancellationToken);
        return Results.Ok(new { message = "Notification queued for dispatch.", envelope });
    }
}

public class DebugNotificationRequest
{
    public string? TenantId { get; set; }
    public string? WorkspaceId { get; set; }
    public Guid? CaseId { get; set; }
    public string? CustomerName { get; set; }
    public decimal? Amount { get; set; }
    public string? Risk { get; set; }
}
