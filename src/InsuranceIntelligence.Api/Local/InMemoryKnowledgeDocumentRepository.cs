using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Local;

public sealed class InMemoryKnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, KnowledgeDocument> documents = new();

    public Task SaveAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        documents[document.KnowledgeDocumentId] = document;
        return Task.CompletedTask;
    }

    public Task<KnowledgeDocument?> GetAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        documents.TryGetValue(knowledgeDocumentId, out var document);
        var match = document is not null && document.Scope.Matches(scope) ? document : null;

        return Task.FromResult(match);
    }

    public Task<IReadOnlyCollection<KnowledgeDocument>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = documents.Values
            .Where(document => document.Scope.Matches(scope))
            .OrderByDescending(document => document.UpdatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<KnowledgeDocument>>(matches);
    }
}
