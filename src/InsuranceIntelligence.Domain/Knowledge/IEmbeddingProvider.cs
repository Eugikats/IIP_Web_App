namespace InsuranceIntelligence.Domain.Knowledge;

/// <summary>What a model reported using for one call: which model, and how many tokens in and out (null when it did not say).</summary>
public sealed record ModelUsage(string Provider, string? Deployment, string? ModelVersion, int? PromptTokens, int? CompletionTokens);

public sealed record EmbeddingResult(IReadOnlyCollection<float> Vector, ModelUsage? Usage);

public interface IEmbeddingProvider
{
    Task<IReadOnlyCollection<float>> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>The same as <see cref="EmbedAsync"/> and also what the call used, so it can be metered. A provider that cannot say reports no usage.</summary>
    async Task<EmbeddingResult> EmbedWithUsageAsync(string text, CancellationToken cancellationToken = default) =>
        new(await EmbedAsync(text, cancellationToken), null);
}
