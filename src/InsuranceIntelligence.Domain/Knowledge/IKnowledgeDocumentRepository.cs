using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public interface IKnowledgeDocumentRepository
{
    Task SaveAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);

    Task<KnowledgeDocument?> GetAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<KnowledgeDocument>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
