using InsuranceIntelligence.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public class MockTeamsNotificationProvider(ILogger<MockTeamsNotificationProvider> logger) : ITeamsNotificationProvider
{
    public Task<string> SendAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MOCK: Simulated sending Teams notification of type {Type} for Case {CaseId}", 
            notification.NotificationType, notification.CaseId);
        return Task.FromResult("delivered_mock");
    }
}
