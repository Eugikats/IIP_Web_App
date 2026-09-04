using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public sealed class EfNotificationRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), INotificationRepository
{
    public Task SaveAsync(Notification notification, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.Notifications,
            notification.NotificationId,
            notification.Scope,
            Serialize(notification),
            record =>
            {
                record.RecipientUserId = notification.RecipientUserId;
                record.IsRead = notification.IsRead;
                record.CreatedAt = notification.CreatedAt;
            },
            cancellationToken);

    public async Task<Notification?> GetAsync(Guid notificationId, TenantScope scope, string recipientUserId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.Notifications.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.RecipientUserId == recipientUserId, cancellationToken);
        return record is null ? null : Deserialize<Notification>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<Notification>> ListForUserAsync(
        TenantScope scope, string recipientUserId, int limit, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Notifications.AsNoTracking(), scope)
            .Where(record => record.RecipientUserId == recipientUserId)
            .OrderByDescending(record => record.CreatedAt)
            .Take(limit)
            .Select(record => Deserialize<Notification>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public Task<int> CountUnreadForUserAsync(TenantScope scope, string recipientUserId, CancellationToken cancellationToken = default) =>
        Scoped(DbContext.Notifications.AsNoTracking(), scope)
            .CountAsync(record => record.RecipientUserId == recipientUserId && !record.IsRead, cancellationToken);

    public async Task<bool> ExistsAsync(
        TenantScope scope, string recipientUserId, NotificationType type, Guid caseId, CancellationToken cancellationToken = default)
    {
        // Type is only in the JSON payload (not a typed column, matching the rest of this
        // codebase's scoped-JSON persistence convention) — the candidate set per case/recipient
        // is always small, so filtering it in memory after a scoped/indexed query is cheap.
        var candidates = await Scoped(DbContext.Notifications.AsNoTracking(), scope)
            .Where(record => record.RecipientUserId == recipientUserId)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        return candidates
            .Select(Deserialize<Notification>)
            .Any(notification => notification.Type == type && notification.CaseId == caseId);
    }
}
