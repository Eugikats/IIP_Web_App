using InsuranceIntelligence.Application.Connectors;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Connectors;

/// <summary>
/// Once every few minutes, runs each active connector whose last run is older than its interval. Each run happens in its
/// own scope, one connector at a time, so a slow provider or a large folder cannot starve the rest of the app on a small
/// plan. A failure in one connector is logged without its details and never stops the others. Interval is
/// <c>Connectors:SyncIntervalMinutes</c> (default 60); <c>Connectors:SyncEnabled=false</c> switches the schedule off,
/// leaving "sync now" in the admin screen.
/// </summary>
public sealed class ConnectorSyncWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<ConnectorSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["Connectors:SyncEnabled"], out var enabled)) enabled = true;
        if (!enabled) return;

        var interval = TimeSpan.FromMinutes(int.TryParse(configuration["Connectors:SyncIntervalMinutes"], out var minutes) && minutes >= 5 ? minutes : 60);
        try
        {
            // Let startup and the first requests finish before any provider work begins.
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await SweepAsync(interval, stoppingToken);
                await Task.Delay(Tick, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    internal async Task SweepAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        IReadOnlyList<DataConnector> active;
        try
        {
            await using var listScope = scopeFactory.CreateAsyncScope();
            active = await listScope.ServiceProvider.GetRequiredService<IDataConnectorRepository>().ListActiveAcrossWorkspacesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The connector tables may not exist yet on a release that ships ahead of its migration. That is not an error to crash on.
            logger.LogWarning("Connector schedule could not read the connector list: {Type}.", exception.GetType().Name);
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var connector in active.Where(c => c.LastRunAt is null || now - c.LastRunAt >= interval))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var run = await scope.ServiceProvider.GetRequiredService<IConnectorSyncService>().RunAsync(connector, $"connector-sync-{Guid.NewGuid():N}", cancellationToken);
                logger.LogInformation(
                    "Connector {ConnectorId} synced: {Outcome}, {Added} added, {Updated} changed, {Removed} removed. Tenant={TenantId} Workspace={WorkspaceId}",
                    connector.ConnectorId, run.Outcome, run.Added, run.Updated, run.Removed, connector.Scope.TenantId, connector.Scope.WorkspaceId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError("Connector {ConnectorId} failed to sync: {Type}.", connector.ConnectorId, exception.GetType().Name);
            }
        }
    }
}
