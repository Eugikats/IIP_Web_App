namespace InsuranceIntelligence.Domain.Knowledge;

public interface IRetrievalProvider
{
    Task<RetrievalResult> RetrieveAsync(AssistantQuestion question, CancellationToken cancellationToken = default);
}
