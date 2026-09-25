using System.Collections.Concurrent;
using InsuranceIntelligence.Application.Translation;

namespace InsuranceIntelligence.LocalTestDoubles;

/// <summary>Process-local, non-durable cache: correct for tests, Development and a single-instance
/// Demo deployment. Outcome G can replace the registration with a durable, shared cache without
/// changing <see cref="TranslationService"/>, which only knows <see cref="ITranslationCache"/>.</summary>
public sealed class InMemoryTranslationCache : ITranslationCache
{
    private readonly ConcurrentDictionary<string, string> store = new();

    public Task<string?> GetAsync(string cacheKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.TryGetValue(cacheKey, out var value) ? value : null);

    public Task SetAsync(string cacheKey, string translatedText, CancellationToken cancellationToken = default)
    {
        store[cacheKey] = translatedText;
        return Task.CompletedTask;
    }
}
