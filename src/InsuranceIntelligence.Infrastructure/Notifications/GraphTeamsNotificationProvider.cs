using System.Net.Http.Json;
using InsuranceIntelligence.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Notifications;

// STUB implementation. To be filled out when Graph API access is provisioned.
public class GraphTeamsNotificationProvider(ILogger<GraphTeamsNotificationProvider> logger) : ITeamsNotificationProvider
{
    public async Task<string> SendAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
    {
        // ... (This would contain the Graph API implementation for sending chat messages to Teams)
        logger.LogInformation("Sending Teams notification via Microsoft Graph for Case {CaseId}", notification.CaseId);
        return await Task.FromResult("delivered_graph_simulated");
    }
}
