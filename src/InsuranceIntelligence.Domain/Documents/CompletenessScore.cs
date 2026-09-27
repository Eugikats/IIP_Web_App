namespace InsuranceIntelligence.Domain.Documents;

public sealed record CompletenessScore(decimal Percentage, int RequiredReceived, int RequiredTotal)
{
    public static CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments)
    {
        var receivedSet = receivedDocuments
            .Where(documentType => documentType != DocumentType.Unknown)
            .ToHashSet();
        var receivedRequired = requiredDocuments.Count(receivedSet.Contains);
        return Calculate(requiredDocuments.Count, receivedRequired);
    }

    /// <summary>Same calculation, but counting a product's custom (non-enum) document
    /// requirements alongside the built-in ones (claim intake build prompt outcome A), so a
    /// product whose matrix is mostly custom types does not read as 100% complete with nothing
    /// uploaded. <paramref name="customRequiredTotal"/> and <paramref name="customReceived"/> are
    /// the counts <see cref="DocumentChecklistRuleSet.CustomRequired"/> resolves to against the
    /// case's documents; pass zero for both where there are none, which reproduces the original
    /// two-collection calculation exactly.</summary>
    public static CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments,
        int customRequiredTotal,
        int customReceived)
    {
        var receivedSet = receivedDocuments
            .Where(documentType => documentType != DocumentType.Unknown)
            .ToHashSet();
        var receivedRequired = requiredDocuments.Count(receivedSet.Contains);
        return Calculate(requiredDocuments.Count + customRequiredTotal, receivedRequired + customReceived);
    }

    private static CompletenessScore Calculate(int requiredTotal, int received)
    {
        if (requiredTotal == 0)
        {
            return new CompletenessScore(100m, 0, 0);
        }

        var percentage = Math.Round((decimal)received / requiredTotal * 100m, 2);
        return new CompletenessScore(percentage, received, requiredTotal);
    }
}
