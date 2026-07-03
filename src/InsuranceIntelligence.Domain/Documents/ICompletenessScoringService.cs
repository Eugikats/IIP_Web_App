namespace InsuranceIntelligence.Domain.Documents;

public interface ICompletenessScoringService
{
    CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments);
}
