using InsuranceIntelligence.Application.Rules;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Rules;

/// <summary>
/// Every few minutes, lets each workspace's approved rules look at events that have arrived and at messages that were waiting
/// for their time. Each workspace runs in its own scope and a failure in one never stops the others. A workspace with no
/// approved rule costs one cheap read. <c>Rules:RunEnabled=false</c> switches the schedule off; the Rules screen can still
/// run them on request.
/// </summary>
public sealed class RuleRunWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<RuleRunWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["Rules:RunEnabled"], out var enabled)) enabled = true;
        if (!enabled) return;
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await SweepAsync(stoppingToken);
                await Task.Delay(Tick, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantScope> workspaces;
        try
        {
            await using var listScope = scopeFactory.CreateAsyncScope();
            workspaces = await listScope.ServiceProvider.GetRequiredService<IWorkspaceDirectory>().ListActiveWorkspacesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("The rule schedule could not list workspaces: {Type}.", exception.GetType().Name);
            return;
        }

        foreach (var workspace in workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var summary = await scope.ServiceProvider.GetRequiredService<RuleEngine>().RunAsync(workspace, "system:rule-schedule", cancellationToken);
                if (summary.Considered > 0)
                {
                    logger.LogInformation(
                        "Rules ran: {Considered} considered, {Sent} sent, {Skipped} skipped, {Scheduled} waiting, {Blocked} blocked. Tenant={TenantId} Workspace={WorkspaceId}",
                        summary.Considered, summary.Sent + summary.DemoSent, summary.Skipped, summary.Scheduled, summary.Blocked, workspace.TenantId, workspace.WorkspaceId);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The workspace tables may not exist yet on a release that ships ahead of its migration.
                logger.LogWarning("Rules could not run for a workspace: {Type}. Tenant={TenantId}", exception.GetType().Name, workspace.TenantId);
            }
        }
    }
}
