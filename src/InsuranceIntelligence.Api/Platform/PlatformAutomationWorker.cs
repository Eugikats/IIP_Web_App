using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Platform;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Platform;

/// <summary>
/// Runs each workspace's active, approved platform automations when they are due. Each workspace runs in its own scope with
/// its own tenant context, so a run reads only that workspace's data, and a failure in one never stops the others. A workspace
/// with no active automation costs one cheap read. <c>Platform:AutomationsEnabled=false</c> switches the schedule off;
/// the Automations screen can still run one on request. The poll interval is <c>Platform:AutomationPollSeconds</c> (30 to 900).
/// </summary>
public sealed class PlatformAutomationWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<PlatformAutomationWorker> logger) : BackgroundService
{
    public const string SystemUser = "system:platform-scheduler";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["Platform:AutomationsEnabled"], out var enabled)) enabled = true;
        if (!enabled)
        {
            logger.LogInformation("Platform automations are switched off (Platform:AutomationsEnabled=false).");
            return;
        }

        var seconds = int.TryParse(configuration["Platform:AutomationPollSeconds"], out var configured) ? Math.Clamp(configured, 30, 900) : 120;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await SweepAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantScope> workspaces;
        try
        {
            await using var listScope = scopeFactory.CreateAsyncScope();
            workspaces = await listScope.ServiceProvider.GetRequiredService<IWorkspaceDirectory>().ListActiveWorkspacesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("The platform schedule could not list workspaces: {Type}.", exception.GetType().Name);
            return 0;
        }

        var total = 0;
        foreach (var workspace in workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
                    .SetCurrent(new TenantContext(workspace.TenantId, workspace.WorkspaceId, SystemUser, []));
                var ran = await scope.ServiceProvider.GetRequiredService<IPlatformAutomationService>().RunDueAsync(cancellationToken);
                if (ran > 0)
                {
                    total += ran;
                    logger.LogInformation("Platform automations ran: {Count}. Tenant={TenantId} Workspace={WorkspaceId}", ran, workspace.TenantId, workspace.WorkspaceId);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The platform tables may not exist yet on a release that ships ahead of its migration.
                logger.LogWarning("Platform automations could not run for a workspace: {Type}. Tenant={TenantId}", exception.GetType().Name, workspace.TenantId);
            }
        }

        return total;
    }
}
