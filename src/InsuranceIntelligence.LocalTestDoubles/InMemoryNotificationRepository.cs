using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<Guid, Notification> notifications = new();

    public Task SaveAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notifications[notification.NotificationId] = notification;
        return Task.CompletedTask;
    }

    public Task<Notification?> GetAsync(Guid notificationId, TenantScope scope, string recipientUserId, CancellationToken cancellationToken = default)
    {
        var found = notifications.TryGetValue(notificationId, out var notification)
            && notification.Scope.Matches(scope)
            && notification.RecipientUserId == recipientUserId
                ? notification
                : null;
        return Task.FromResult(found);
    }

    public Task<IReadOnlyCollection<Notification>> ListForUserAsync(
        TenantScope scope, string recipientUserId, int limit, CancellationToken cancellationToken = default)
    {
        var matches = notifications.Values
            .Where(item => item.Scope.Matches(scope) && item.RecipientUserId == recipientUserId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<Notification>>(matches);
    }

    public Task<int> CountUnreadForUserAsync(TenantScope scope, string recipientUserId, CancellationToken cancellationToken = default)
    {
        var count = notifications.Values
            .Count(item => item.Scope.Matches(scope) && item.RecipientUserId == recipientUserId && !item.IsRead);
        return Task.FromResult(count);
    }

    public Task<bool> ExistsAsync(
        TenantScope scope, string recipientUserId, NotificationType type, Guid caseId, CancellationToken cancellationToken = default)
    {
        var exists = notifications.Values.Any(item =>
            item.Scope.Matches(scope) && item.RecipientUserId == recipientUserId && item.Type == type && item.CaseId == caseId);
        return Task.FromResult(exists);
    }
}
