namespace InsuranceIntelligence.Domain.Knowledge;

public interface IEmbeddingProvider
{
    Task<IReadOnlyCollection<float>> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
