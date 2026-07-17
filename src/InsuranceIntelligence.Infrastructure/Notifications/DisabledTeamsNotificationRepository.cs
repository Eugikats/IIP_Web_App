using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public sealed class DisabledTeamsNotificationRepository : ITeamsNotificationRepository
{
    public Task SaveAsync(TeamsNotification notification, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Teams notification persistence is not configured. Configure SqlServer storage before queueing notifications.");

    public Task<IEnumerable<TeamsNotification>> ListAsync(
        TenantScope scope,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TeamsNotification>>([]);
}
