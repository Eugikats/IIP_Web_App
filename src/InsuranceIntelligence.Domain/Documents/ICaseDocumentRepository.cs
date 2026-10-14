using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public interface ICaseDocumentRepository
{
    Task SaveAsync(CaseDocument document, CancellationToken cancellationToken = default);

    Task<CaseDocument?> GetAsync(Guid caseId, Guid documentId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CaseDocument>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// The documents of many cases in one read, so a list of cases does not cost one query per case. The default simply
    /// asks case by case; the database repository overrides it with a single query.
    /// </summary>
    async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<CaseDocument>>> ListForCasesAsync(
        IReadOnlyCollection<Guid> caseIds, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, IReadOnlyCollection<CaseDocument>>();
        foreach (var caseId in caseIds.Distinct())
        {
            result[caseId] = await ListForCaseAsync(caseId, scope, cancellationToken);
        }

        return result;
    }
}
