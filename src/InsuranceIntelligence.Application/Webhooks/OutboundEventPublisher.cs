using System.Text.Json;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Webhooks;

namespace InsuranceIntelligence.Application.Webhooks;

/// <summary>
/// Turns the audit trail into events for the insurer's own systems. The audit trail is already the
/// record of everything that matters, so events come from it rather than from a second set of hooks
/// that could drift from it. An event carries identifiers and statuses only: no names, phone numbers
/// or claim narratives leave through a webhook. The receiver fetches whatever else it is allowed to
/// see through the API.
/// </summary>
public sealed class OutboundEventPublisher(IOutboundWebhookRepository repository)
{
    public async Task PublishFromAuditAsync(AuditEvent audit, CancellationToken cancellationToken = default)
    {
        var type = OutboundEventTypes.FromAuditAction(audit.ActionName);
        if (type is null)
        {
            return;
        }

        var subscriptions = (await repository.ListSubscriptionsAsync(audit.Scope, cancellationToken)).Where(s => s.Wants(type)).ToArray();
        if (subscriptions.Length == 0)
        {
            return;
        }

        var body = BuildBody(audit.EventId.ToString(), type, audit.Timestamp, audit, null);
        var now = DateTimeOffset.UtcNow;
        foreach (var subscription in subscriptions)
        {
            await repository.SaveDeliveryAsync(
                new OutboundDelivery(Guid.NewGuid(), audit.Scope, subscription.SubscriptionId, audit.EventId.ToString(), type, body,
                    OutboundDeliveryStatus.Pending, 0, now, now),
                cancellationToken);
        }
    }

    /// <summary>A test event, so an integrator can check the connection and the signature without waiting for a claim.</summary>
    public async Task<OutboundDelivery> QueuePingAsync(OutboundWebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid().ToString();
        var delivery = new OutboundDelivery(
            Guid.NewGuid(), subscription.Scope, subscription.SubscriptionId, eventId, OutboundEventTypes.Ping,
            BuildBody(eventId, OutboundEventTypes.Ping, now, null, subscription), OutboundDeliveryStatus.Pending, 0, now, now);
        await repository.SaveDeliveryAsync(delivery, cancellationToken);
        return delivery;
    }

    public static string BuildBody(string eventId, string type, DateTimeOffset occurredAt, AuditEvent? audit, OutboundWebhookSubscription? subscription)
    {
        var scope = audit?.Scope ?? subscription!.Scope;
        object data = audit is null
            ? new { message = "This is a test event from IIP." }
            : new
            {
                claimId = audit.CaseId,
                claimReference = audit.CaseId is { } id ? ClaimReference.For(id) : null,
                action = audit.ActionName,
                previousStatus = audit.BeforeStatus,
                status = audit.AfterStatus,
            };
        return JsonSerializer.Serialize(
            new { id = eventId, type, occurredAt, organisationId = scope.TenantId, workspaceId = scope.WorkspaceId, data },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
