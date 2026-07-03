using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public interface IKnowledgeIndexProvider
{
    Task IndexAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default);

    Task RebuildAsync(TenantScope scope, IReadOnlyCollection<KnowledgeDocument> approvedDocuments, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<KnowledgeChunk>> SearchAsync(TenantScope scope, string query, string? productLine, CancellationToken cancellationToken = default);
}
