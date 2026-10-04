using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

/// <summary>
/// The real "scheduled enforcement job" blueprint §14 Phase 6 WO 6.3 asks for. Runs the exact same
/// RetentionEnforcementService the manual RetentionEndpoints.EnforceRetention path uses, the
/// scheduled and manual triggers are provably the same code, not two implementations that can
/// drift apart.
///
/// Resolves its tenant scope from the deployment's own InfrastructureOptions.TenantId/WorkspaceId,
/// the same "one Managed Application per insurer" single-tenant-per-deployment pattern
/// WhatsAppOptions already uses, there is no cross-tenant "list every tenant" capability
/// anywhere in this codebase's repositories to iterate instead, because this edition of the
/// platform does not have one.
/// </summary>
public class RetentionEnforcementTimerFunction(
    IRetentionEnforcementService enforcementService,
    InsuranceIntelligence.Domain.Common.IWorkspaceDirectory workspaceDirectory,
    InfrastructureOptions options,
    ILogger<RetentionEnforcementTimerFunction> logger)
{
    // Once a day at 02:00 UTC, a low-traffic window, and daily is frequent enough that an expired
    // record never sits around materially longer than its own configured retention window.
    [Function("RetentionEnforcement")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var workspaces = await ScheduledWorkspaces.ResolveAsync(options, workspaceDirectory, logger, "Retention enforcement", cancellationToken);
        var correlationId = $"retention-timer-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        await ScheduledWorkspaces.ForEachAsync(workspaces, logger, "Retention enforcement", async scope =>
        {
            logger.LogInformation("Retention enforcement run starting. Tenant={TenantId} Workspace={WorkspaceId} CorrelationId={CorrelationId}",
                scope.TenantId, scope.WorkspaceId, correlationId);

            var results = await enforcementService.EnforceAsync(scope, correlationId, cancellationToken);

            foreach (var result in results)
            {
                logger.LogInformation(
                    "Retention enforcement: {DataClass} deleted {Count} record(s) (retention {Days} days applied).",
                    result.DataClass, result.RecordsDeleted, result.RetentionDaysApplied);
            }
        });
    }
}
