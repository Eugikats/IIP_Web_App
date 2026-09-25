using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.Translation;

/// <summary>The adapter boundary for the AfriSLM container (outcome G). Returns the model's raw
/// output, uncleaned and unjudged, that is <see cref="TranslationQualityJudge"/>'s job, not the
/// provider's; a provider that started cleaning its own output would hide failures the judge is
/// supposed to catch.</summary>
public interface ITranslationProvider
{
    /// <summary>False when no engine is reachable (build plan §"six states", "not configured");
    /// <see cref="Application.Translation.TranslationService"/> falls back to English rather than
    /// calling a provider that cannot serve the request.</summary>
    bool IsConfigured { get; }

    Task<string> TranslateAsync(
        string text,
        SupportedLanguageCode from,
        SupportedLanguageCode to,
        CancellationToken cancellationToken = default);
}
