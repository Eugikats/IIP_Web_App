using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Application.Compliance;

public interface IRetentionEnforcementService
{
    /// <summary>Runs retention enforcement for every real data class this platform has a delete
    /// path for (blueprint §14 Phase 6, WO 6.3), for one tenant scope. Always writes one audit
    /// event per data class per run, "the retention job deletes an expired record and the
    /// deletion is in the audit trail" is this WO's own exit test, and a run that deleted nothing
    /// still writes its own honest "0 deleted" event, so a quiet period is provable, not
    /// indistinguishable from the job never having run at all.</summary>
    Task<IReadOnlyCollection<RetentionEnforcementResult>> EnforceAsync(
        TenantScope scope,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class RetentionEnforcementService(
    IRetentionPolicyRepository policyRepository,
    ICustomerConversationRepository conversationRepository,
    IAuditRepository auditRepository,
    ILegalHoldRepository? legalHolds = null) : IRetentionEnforcementService
{
    private const string SystemActorId = "system:retention-enforcement";

    public async Task<IReadOnlyCollection<RetentionEnforcementResult>> EnforceAsync(
        TenantScope scope,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<RetentionEnforcementResult>();
        var hold = legalHolds is null ? null : await legalHolds.GetAsync(scope, cancellationToken);

        foreach (var dataClass in Enum.GetValues<RetentionDataClass>())
        {
            var policy = await policyRepository.GetAsync(scope, dataClass, cancellationToken);
            var retentionDays = policy?.RetentionDays ?? RetentionPolicy.DefaultRetentionDays(dataClass);
            var now = DateTimeOffset.UtcNow;
            var cutoff = now.AddDays(-retentionDays);

            // A legal hold means nothing is deleted, and the run says so, so a held period is provable.
            var deletion = hold is { Active: true }
                ? RetentionDeletionSummary.None
                : dataClass switch
                {
                    RetentionDataClass.CustomerConversationTurns => await conversationRepository.DeleteOlderThanAsync(scope, cutoff, cancellationToken),
                    _ => RetentionDeletionSummary.None,
                };

            var result = new RetentionEnforcementResult(
                dataClass, retentionDays, deletion.Count, deletion.OldestOccurredAt, deletion.NewestOccurredAt, now);
            results.Add(result);

            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, null, null, SystemActorId, ActorType.Integration,
                    "retention.enforced", now,
                    null, null, [], "RetentionEnforcementService", null,
                    hold is { Active: true } ? "held" : deletion.Count > 0 ? "deleted" : "no_expired_records",
                    null,
                    hold is { Active: true }
                        ? $"Retention enforcement for {dataClass} did not run: a legal hold is in place."
                        : $"Retention enforcement for {dataClass} (retention {retentionDays} days, cutoff {cutoff:O}): {deletion.Count} record(s) deleted"
                          + (deletion.Count > 0 ? $" (oldest {deletion.OldestOccurredAt:O}, newest {deletion.NewestOccurredAt:O})." : "."),
                    correlationId),
                cancellationToken);
        }

        return results;
    }
}
