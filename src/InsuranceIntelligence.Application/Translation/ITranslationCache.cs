namespace InsuranceIntelligence.Application.Translation;

/// <summary>Keyed by <see cref="TranslationService.BuildCacheKey"/>, which folds in the model tag
/// (file name and size), direction, language and normalised text, so a new model quant never
/// serves a cached translation from the one it replaced.</summary>
public interface ITranslationCache
{
    Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);

    Task SetAsync(string cacheKey, string translatedText, CancellationToken cancellationToken = default);
}
