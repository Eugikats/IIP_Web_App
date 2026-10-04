using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>Once a minute, finds customer messages that were saved but never answered (the process
/// restarted in between) and gives them to the assistant. See <see cref="WhatsAppAssistantRecoveryService"/>.</summary>
public sealed class WhatsAppAssistantRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    InfrastructureOptions options,
    ILogger<WhatsAppAssistantRecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup finish, and let any message that was mid-flight at the restart become old enough.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await SweepAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetRequiredService<IWhatsAppRuntimeSettingsProvider>();
            if (!settings.AnyConfigured)
            {
                return;
            }

            var workspaces = options.MultiTenant
                ? await scope.ServiceProvider.GetRequiredService<IWorkspaceDirectory>().ListActiveWorkspacesAsync(cancellationToken)
                : settings.All.Select(s => TenantScope.Create(s.TenantId, s.WorkspaceId)).ToArray();

            foreach (var workspace in workspaces.Where(w => settings.ForTenant(w) is not null))
            {
                try
                {
                    await using var workspaceScope = scopeFactory.CreateAsyncScope();
                    var recovered = await workspaceScope.ServiceProvider.GetRequiredService<WhatsAppAssistantRecoveryService>()
                        .RecoverAsync(workspace, options.WhatsApp.AssistantAutoReplyEnabled, DateTimeOffset.UtcNow, cancellationToken);
                    if (recovered > 0)
                    {
                        logger.LogWarning(
                            "Recovered {Count} customer message(s) that were saved but never answered. Tenant={TenantId} Workspace={WorkspaceId}",
                            recovered, workspace.TenantId, workspace.WorkspaceId);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "WhatsApp recovery failed for Tenant={TenantId} Workspace={WorkspaceId}", workspace.TenantId, workspace.WorkspaceId);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "WhatsApp recovery sweep failed.");
        }
    }
}
