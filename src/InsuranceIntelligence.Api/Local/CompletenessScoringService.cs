using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Local;

public sealed class CompletenessScoringService : ICompletenessScoringService
{
    public CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments) =>
        CompletenessScore.Calculate(requiredDocuments, receivedDocuments);
}
