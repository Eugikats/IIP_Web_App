namespace InsuranceIntelligence.Domain.Documents;

public sealed record CompletenessScore(decimal Percentage, int RequiredReceived, int RequiredTotal)
{
    public static CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments)
    {
        if (requiredDocuments.Count == 0)
        {
            return new CompletenessScore(100m, 0, 0);
        }

        var receivedSet = receivedDocuments
            .Where(documentType => documentType != DocumentType.Unknown)
            .ToHashSet();
        var receivedRequired = requiredDocuments.Count(receivedSet.Contains);
        var percentage = Math.Round((decimal)receivedRequired / requiredDocuments.Count * 100m, 2);

        return new CompletenessScore(percentage, receivedRequired, requiredDocuments.Count);
    }
}
