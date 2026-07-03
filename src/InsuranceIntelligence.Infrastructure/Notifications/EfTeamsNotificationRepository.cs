using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public class EfTeamsNotificationRepository(InsuranceIntelligenceDbContext dbContext) : ITeamsNotificationRepository
{
    public async Task SaveAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TeamsNotifications
            .FirstOrDefaultAsync(item => item.Id == notification.Id, cancellationToken);

        if (existing is null)
        {
            dbContext.TeamsNotifications.Add(new PersistedTeamsNotification
            {
                Id = notification.Id,
                TenantId = notification.Scope.TenantId,
                WorkspaceId = notification.Scope.WorkspaceId,
                CaseId = notification.CaseId,
                PayloadJson = notification.PayloadJson,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.CaseId = notification.CaseId;
            existing.PayloadJson = notification.PayloadJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<TeamsNotification>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var notifications = await dbContext.TeamsNotifications
            .Where(item => item.TenantId == scope.TenantId && item.WorkspaceId == scope.WorkspaceId)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);

        return notifications.Select(item => new TeamsNotification
        {
            Id = item.Id,
            Scope = scope,
            CaseId = item.CaseId,
            NotificationType = TeamsNotificationType.ApprovalRequired, // We might need to store this or parse from PayloadJson
            PayloadJson = item.PayloadJson
        });
    }
}
