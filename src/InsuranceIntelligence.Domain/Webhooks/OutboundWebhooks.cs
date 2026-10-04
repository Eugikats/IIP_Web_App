using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Webhooks;

/// <summary>The events an insurer's own systems can subscribe to. Names are a public contract:
/// they are only ever added to.</summary>
public static class OutboundEventTypes
{
    public const string Ping = "ping";
    public const string ClaimCreated = "claim.created";
    public const string ClaimStatusChanged = "claim.status_changed";
    public const string ClaimAssigned = "claim.assigned";
    public const string ClaimDecisionRecorded = "claim.decision_recorded";
    public const string ClaimDocumentAdded = "claim.document_added";

    public static readonly IReadOnlyList<string> All =
        [ClaimCreated, ClaimStatusChanged, ClaimAssigned, ClaimDecisionRecorded, ClaimDocumentAdded];

    /// <summary>Which event, if any, an audit entry becomes. Anything not listed is internal and never leaves.</summary>
    public static string? FromAuditAction(string? action) => action switch
    {
        "case.created" => ClaimCreated,
        "case.status.updated" => ClaimStatusChanged,
        "case.assigned" => ClaimAssigned,
        "approval.approved" or "approval.rejected" or "approval.overridden" => ClaimDecisionRecorded,
        "document.uploaded" => ClaimDocumentAdded,
        _ => null,
    };

    public static bool IsKnown(string type) => type == "*" || type == Ping || All.Contains(type, StringComparer.Ordinal);
}

public sealed record OutboundWebhookSubscription(
    Guid SubscriptionId,
    TenantScope Scope,
    string Url,
    string EncryptedSecret,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastDeliveredAt = null,
    int ConsecutiveFailures = 0)
{
    public bool Wants(string eventType) =>
        IsActive && (EventTypes.Contains("*", StringComparer.Ordinal) || EventTypes.Contains(eventType, StringComparer.Ordinal));
}

public enum OutboundDeliveryStatus
{
    Pending,
    Delivered,

    /// <summary>Given up after the last retry. Kept so the insurer can see it and ask for a replay.</summary>
    Dead,
}

public sealed record OutboundDelivery(
    Guid DeliveryId,
    TenantScope Scope,
    Guid SubscriptionId,
    string EventId,
    string EventType,
    string Body,
    OutboundDeliveryStatus Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? DeliveredAt = null,
    int? LastStatusCode = null,
    string? LastError = null);

public interface IOutboundWebhookRepository
{
    Task SaveSubscriptionAsync(OutboundWebhookSubscription subscription, CancellationToken cancellationToken = default);
    Task<OutboundWebhookSubscription?> GetSubscriptionAsync(TenantScope scope, Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboundWebhookSubscription>> ListSubscriptionsAsync(TenantScope scope, CancellationToken cancellationToken = default);
    Task DeleteSubscriptionAsync(TenantScope scope, Guid subscriptionId, CancellationToken cancellationToken = default);

    Task SaveDeliveryAsync(OutboundDelivery delivery, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboundDelivery>> ListDeliveriesAsync(TenantScope scope, Guid subscriptionId, int take, CancellationToken cancellationToken = default);

    /// <summary>Deliveries due now, across every organisation. Platform-level: only the dispatcher calls it.</summary>
    Task<IReadOnlyList<OutboundDelivery>> ListDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default);
}
