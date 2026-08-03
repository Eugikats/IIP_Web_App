using System.Net.Http.Json;
using InsuranceIntelligence.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public class IncomingWebhookTeamsProviderOptions
{
    public string WebhookUrl { get; set; } = string.Empty;
}

public class IncomingWebhookTeamsProvider(
    HttpClient httpClient,
    IOptions<IncomingWebhookTeamsProviderOptions> options,
    ILogger<IncomingWebhookTeamsProvider> logger) : ITeamsNotificationProvider
{
    public async Task<string> SendAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
    {
        var webhookUrl = options.Value.WebhookUrl;
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new InvalidOperationException("Teams incoming webhook is not configured.");
        }

        // Build the Adaptive Card JSON
        var cardJson = TeamsAdaptiveCardBuilder.BuildCard(notification);
        var cardElement = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(cardJson);

        var payload = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    contentUrl = (string?)null,
                    content = cardElement
                }
            }
        };

        var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Successfully sent Teams notification via Incoming Webhook for case {CaseId}.", notification.CaseId);
        return "delivered_webhook";
    }
}
