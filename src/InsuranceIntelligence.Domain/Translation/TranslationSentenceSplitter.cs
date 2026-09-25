using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Ports the reference app's <c>splitTranslationUnits</c> (build plan §5): the model
/// degrades on long input, so anything past the threshold is translated sentence by sentence and
/// the pieces are rejoined, rather than risking one long call that comes back garbled.</summary>
public static class TranslationSentenceSplitter
{
    private const int SplitThreshold = 72;

    private static readonly Regex SentenceBoundary = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);

    public static IReadOnlyList<string> Split(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= SplitThreshold)
        {
            return [text];
        }

        var sentences = SentenceBoundary.Split(text)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToList();

        return sentences.Count > 0 ? sentences : [text];
    }
}
