using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Notifications;

public enum TeamsNotificationDeliveryStatus
{
    Queued,
    Delivered,
    Failed
}

public class TeamsNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TenantScope Scope { get; set; } = null!;
    public Guid? CaseId { get; set; }
    public TeamsNotificationType NotificationType { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string DeepLinkUrl { get; set; } = string.Empty;
    public TeamsNotificationDeliveryStatus DeliveryStatus { get; set; } = TeamsNotificationDeliveryStatus.Queued;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
    
    // Concurrency token for optimistic concurrency when updating status
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
