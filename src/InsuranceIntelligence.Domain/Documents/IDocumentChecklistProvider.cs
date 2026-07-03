using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Domain.Documents;

public interface IDocumentChecklistProvider
{
    Task<DocumentCompletenessChecklist> BuildChecklistAsync(
        ClaimCase claimCase,
        IReadOnlyCollection<CaseDocument> documents,
        CancellationToken cancellationToken = default);
}
