using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;

namespace InsuranceIntelligence.Application.Notifications;

/// <summary>
/// Emits real in-app notifications from real events elsewhere in the system (approval assigned,
/// claim escalated, SLA breach, document extraction needing review, review requested) and serves
/// the recipient's own feed. Never called with synthesised content (AGENTS.md A5).
/// </summary>
public interface INotificationEmitter
{
    Task NotifyAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="NotifyAsync"/> but skips creating a duplicate when a
    /// notification of this type already exists for this recipient/case — for events re-derived
    /// from a live condition on every read (SLA breach) rather than a one-shot action.</summary>
    Task NotifyOnceAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default);
}

public interface INotificationFeedService
{
    Task<NotificationFeedResponse> GetFeedAsync(int limit, CancellationToken cancellationToken = default);

    Task<NotificationDto> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
}

public sealed record NotificationDto(
    Guid NotificationId,
    string Type,
    string Message,
    Guid CaseId,
    DateTimeOffset CreatedAt,
    bool IsRead,
    DateTimeOffset? ReadAt);

public sealed record NotificationFeedResponse(
    int UnreadCount,
    IReadOnlyCollection<NotificationDto> Notifications);

public sealed class NotificationNotFoundException : Exception;

public sealed class NotificationService(
    ITenantContextAccessor tenantContextAccessor,
    INotificationRepository repository) : INotificationEmitter, INotificationFeedService
{
    public Task NotifyAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            // No known recipient (e.g. an unassigned approval) — nothing to notify. Not an error;
            // the caller's own action still completes.
            return Task.CompletedTask;
        }

        return repository.SaveAsync(Build(scope, recipientUserId, type, message, caseId), cancellationToken);
    }

    public async Task NotifyOnceAsync(
        TenantScope scope,
        string recipientUserId,
        NotificationType type,
        string message,
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            return;
        }

        if (await repository.ExistsAsync(scope, recipientUserId, type, caseId, cancellationToken))
        {
            return;
        }

        await repository.SaveAsync(Build(scope, recipientUserId, type, message, caseId), cancellationToken);
    }

    private static Notification Build(TenantScope scope, string recipientUserId, NotificationType type, string message, Guid caseId) =>
        new(
            Guid.NewGuid(),
            scope,
            recipientUserId,
            type,
            message,
            caseId,
            DateTimeOffset.UtcNow,
            IsRead: false,
            ReadAt: null);

    public async Task<NotificationFeedResponse> GetFeedAsync(int limit, CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var unreadCount = await repository.CountUnreadForUserAsync(context.Scope, context.UserId, cancellationToken);
        var notifications = await repository.ListForUserAsync(context.Scope, context.UserId, boundedLimit, cancellationToken);
        return new NotificationFeedResponse(unreadCount, notifications.Select(ToDto).ToArray());
    }

    public async Task<NotificationDto> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var notification = await repository.GetAsync(notificationId, context.Scope, context.UserId, cancellationToken)
            ?? throw new NotificationNotFoundException();

        if (notification.IsRead)
        {
            return ToDto(notification);
        }

        var read = notification with { IsRead = true, ReadAt = DateTimeOffset.UtcNow };
        await repository.SaveAsync(read, cancellationToken);
        return ToDto(read);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var unread = (await repository.ListForUserAsync(context.Scope, context.UserId, 100, cancellationToken))
            .Where(item => !item.IsRead)
            .ToArray();

        foreach (var notification in unread)
        {
            await repository.SaveAsync(notification with { IsRead = true, ReadAt = DateTimeOffset.UtcNow }, cancellationToken);
        }

        return unread.Length;
    }

    private static NotificationDto ToDto(Notification notification) => new(
        notification.NotificationId,
        notification.Type.ToString(),
        notification.Message,
        notification.CaseId,
        notification.CreatedAt,
        notification.IsRead,
        notification.ReadAt);
}
