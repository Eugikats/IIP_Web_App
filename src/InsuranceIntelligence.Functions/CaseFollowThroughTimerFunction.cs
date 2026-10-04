using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

/// <summary>
/// "Follow through without a person remembering" (build prompt outcome G): a customer reminder
/// once documents have been missing for the configured wait, and an officer warning before an SLA
/// breach rather than after it. Runs the exact same ICaseFollowThroughService the manual
/// CaseFollowThroughEndpoints.Run path uses, same pattern as RetentionEnforcementTimerFunction:
/// scheduled and manual triggers are provably the same code.
///
/// Resolves its tenant scope from the deployment's own InfrastructureOptions.TenantId/WorkspaceId
/// (this edition is one Managed Application per insurer, no cross-tenant enumeration capability
/// exists to iterate instead).
/// </summary>
public class CaseFollowThroughTimerFunction(
    ICaseFollowThroughService followThroughService,
    InsuranceIntelligence.Domain.Common.IWorkspaceDirectory workspaceDirectory,
    InfrastructureOptions options,
    ILogger<CaseFollowThroughTimerFunction> logger)
{
    // Every 6 hours: frequent enough that a warning fired within its 24-hour lead time is never
    // more than a few hours late, without hammering providers or the audit trail every run.
    [Function("CaseFollowThrough")]
    public async Task Run([TimerTrigger("0 0 */6 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var workspaces = await ScheduledWorkspaces.ResolveAsync(options, workspaceDirectory, logger, "Case follow-through", cancellationToken);
        var correlationId = $"follow-through-timer-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        await ScheduledWorkspaces.ForEachAsync(workspaces, logger, "Case follow-through", async scope =>
        {
            logger.LogInformation("Case follow-through run starting. Tenant={TenantId} Workspace={WorkspaceId} CorrelationId={CorrelationId}",
                scope.TenantId, scope.WorkspaceId, correlationId);

            var result = await followThroughService.RunAsync(scope, correlationId, cancellationToken);

            logger.LogInformation(
                "Case follow-through: {Reminders} missing-document reminder(s) sent, {Warnings} SLA-at-risk warning(s) sent.",
                result.MissingDocumentRemindersSent, result.SlaWarningsSent);
        });
    }
}
