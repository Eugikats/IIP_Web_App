using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.Translation;

/// <summary>Thin adapter over the pure <see cref="LanguageWordListScorer"/>, the concrete
/// implementation of step 3 of the resolution order (build plan §6.B).</summary>
public sealed class WordListLanguageDetector : ILanguageDetector
{
    public LanguageResolution Detect(string text) => LanguageWordListScorer.Score(text);
}
