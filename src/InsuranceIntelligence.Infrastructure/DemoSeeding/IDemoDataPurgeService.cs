namespace InsuranceIntelligence.Infrastructure.DemoSeeding;

/// <summary>
/// Narrow, explicitly-scoped hard-delete capability used only to remove named legacy
/// test/UAT artifacts before a demo reseed. This is deliberately NOT a general delete
/// path on the core repositories — IIP's case/document/approval/audit repositories have
/// no Delete method by design, because the audit trail must be preservable. This service
/// exists purely so a small, explicit, admin-triggered list of known-bad records can be
/// purged (rows + their dependents) without adding a delete capability anywhere a normal
/// workflow could reach it. Callers must gate this to non-protected runtime profiles and
/// an Admin-only permission (see DemoSeedEndpoints).
/// </summary>
public interface IDemoDataPurgeService
{
    /// <summary>
    /// Removes the given case IDs and every row that references them (documents,
    /// approvals, audit events, workflow tasks, agent workflow runs/work items) within
    /// the given tenant scope. Case IDs that do not exist, or do not belong to the scope,
    /// are ignored. Returns the number of case rows actually removed.
    /// </summary>
    Task<int> PurgeCasesAsync(
        IReadOnlyCollection<Guid> caseIds,
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        CancellationToken cancellationToken = default);
}
