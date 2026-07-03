using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public class InMemoryTeamsNotificationRepository : ITeamsNotificationRepository
{
    private readonly ConcurrentDictionary<Guid, TeamsNotification> _store = new();

    public Task SaveAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
    {
        _store[notification.Id] = notification;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TeamsNotification>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var results = _store.Values
            .Where(item => item.Scope.TenantId == scope.TenantId && item.Scope.WorkspaceId == scope.WorkspaceId)
            .ToList();

        return Task.FromResult<IEnumerable<TeamsNotification>>(results);
    }
}
