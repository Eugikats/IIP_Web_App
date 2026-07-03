using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public interface ICaseDocumentRepository
{
    Task SaveAsync(CaseDocument document, CancellationToken cancellationToken = default);

    Task<CaseDocument?> GetAsync(Guid caseId, Guid documentId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CaseDocument>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
