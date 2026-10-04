using InsuranceIntelligence.Application.Webhooks;

namespace InsuranceIntelligence.Api.Webhooks;

/// <summary>Every few seconds, sends the events that are due. Safe to run on several instances: a
/// delivery is retried at most a little early, and receivers are told to ignore duplicates by delivery id.</summary>
public sealed class OutboundWebhookWorker(IServiceScopeFactory scopeFactory, ILogger<OutboundWebhookWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var sent = await scope.ServiceProvider.GetRequiredService<OutboundWebhookDispatcher>()
                        .DispatchDueAsync(DateTimeOffset.UtcNow, cancellationToken: stoppingToken);
                    await Task.Delay(sent > 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Webhook dispatch failed; will try again shortly.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }
}
