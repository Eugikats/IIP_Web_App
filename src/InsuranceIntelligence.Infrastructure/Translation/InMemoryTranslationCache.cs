using System.Collections.Concurrent;
using InsuranceIntelligence.Application.Translation;

namespace InsuranceIntelligence.Infrastructure.Translation;

/// <summary>Process-local, non-durable, but a real cache, not a fake: a miss just means the next
/// call recomputes the translation, there is no correctness or security property riding on this
/// surviving a restart. Correct for every profile including Staging and Production; outcome G can
/// swap in a shared, durable cache behind the same ITranslationCache without touching callers.</summary>
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
