namespace InsuranceIntelligence.Domain.Documents;

public interface ICompletenessScoringService
{
    CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments);

    /// <summary>Same calculation, counting a product's custom (non-enum) document requirements
    /// alongside the built-in ones (claim intake build prompt outcome A). Pass zero for both custom
    /// counts to reproduce the two-argument overload exactly.</summary>
    CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments,
        int customRequiredTotal,
        int customReceived);
}
