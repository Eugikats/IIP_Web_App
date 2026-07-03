namespace InsuranceIntelligence.Domain.Knowledge;

public interface IKnowledgeStorageProvider
{
    Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default);
}
