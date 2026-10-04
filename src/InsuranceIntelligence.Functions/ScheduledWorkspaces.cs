using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

/// <summary>
/// Which workspaces a scheduled job runs for. A Managed Application has the one it was deployed for;
/// SaaS has every live workspace, each run in its own scope with its own failure handling, so one
/// insurer's problem never stops the others' work.
/// </summary>
public static class ScheduledWorkspaces
{
    public static async Task<IReadOnlyList<TenantScope>> ResolveAsync(
        InfrastructureOptions options, IWorkspaceDirectory directory, ILogger logger, string jobName, CancellationToken cancellationToken)
    {
        if (options.MultiTenant)
        {
            return await directory.ListActiveWorkspacesAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.WorkspaceId))
        {
            logger.LogWarning("{Job} run skipped: no deployment-level TenantId/WorkspaceId is configured.", jobName);
            return [];
        }

        return [TenantScope.Create(options.TenantId, options.WorkspaceId)];
    }

    /// <summary>Runs the job for each workspace; a failure in one is logged and the rest still run.</summary>
    public static async Task ForEachAsync(
        IReadOnlyList<TenantScope> workspaces, ILogger logger, string jobName, Func<TenantScope, Task> run)
    {
        foreach (var scope in workspaces)
        {
            try
            {
                await run(scope);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "{Job} failed for Tenant={TenantId} Workspace={WorkspaceId}; continuing with the others.",
                    jobName, scope.TenantId, scope.WorkspaceId);
            }
        }
    }
}
