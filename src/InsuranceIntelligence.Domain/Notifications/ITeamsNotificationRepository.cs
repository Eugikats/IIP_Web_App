using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Notifications;

public interface ITeamsNotificationRepository
{
    Task SaveAsync(TeamsNotification notification, CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamsNotification>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
