using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Cases;

public interface IAuditRepository
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The audit trails of many cases in one read, so a list of cases does not cost one query per case. The default asks
    /// case by case; the database repository overrides it with a single query.
    /// </summary>
    async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<AuditEvent>>> ListForCasesAsync(
        IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, IReadOnlyCollection<AuditEvent>>();
        foreach (var caseId in caseIds.Distinct())
        {
            result[caseId] = await ListForCaseAsync(caseId, cancellationToken);
        }

        return result;
    }

    Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
