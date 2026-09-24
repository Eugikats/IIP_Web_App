namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// Outcome K acceptance: "accuracy per document type is measured and visible." Deterministic,
/// over real labelled examples (AGENTS.md non-negotiable 2) -- never a model call. Grouped by
/// what the classifier originally predicted (not the confirmed type), so this answers "when the
/// classifier said X, how often was it actually right?" rather than "how often is a real X caught."
/// Only Reclassification and ConfirmedWithoutCorrection examples carry a classification signal;
/// FieldCorrection examples do not change the document's type and are excluded here (they still
/// accumulate as labelled data for extraction-quality work, just not this metric).
/// </summary>
public sealed record DocumentTypeAccuracy(
    DocumentType DocumentType,
    int SampleCount,
    int CorrectCount,
    decimal? Accuracy);

public static class DocumentClassificationAccuracy
{
    /// <summary>Below this many labelled examples for a type, an accuracy figure is not shown
    /// (echoes the custom-classification "five samples per class" minimum outcome K names).</summary>
    public const int MinimumSampleCount = 5;

    public static IReadOnlyList<DocumentTypeAccuracy> ByDocumentType(IEnumerable<DocumentLabelledExample> examples)
    {
        var classificationSignals = examples
            .Where(example => example.Source is DocumentLabelSource.Reclassification or DocumentLabelSource.ConfirmedWithoutCorrection);

        return classificationSignals
            .GroupBy(example => example.OriginalPredictedType)
            .Select(group =>
            {
                var sampleCount = group.Count();
                var correctCount = group.Count(example => example.ConfirmedType == example.OriginalPredictedType);
                var accuracy = sampleCount >= MinimumSampleCount
                    ? Math.Round((decimal)correctCount / sampleCount, 4)
                    : (decimal?)null;
                return new DocumentTypeAccuracy(group.Key, sampleCount, correctCount, accuracy);
            })
            .OrderBy(row => row.DocumentType)
            .ToArray();
    }
}
