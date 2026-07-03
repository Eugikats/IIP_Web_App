using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryCaseDocumentRepository : ICaseDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, CaseDocument> documents = new();

    public Task SaveAsync(CaseDocument document, CancellationToken cancellationToken = default)
    {
        documents[document.DocumentId] = document;
        return Task.CompletedTask;
    }

    public Task<CaseDocument?> GetAsync(Guid caseId, Guid documentId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        documents.TryGetValue(documentId, out var document);
        var match = document is not null
            && document.CaseId == caseId
            && document.Scope.Matches(scope)
            ? document
            : null;

        return Task.FromResult(match);
    }

    public Task<IReadOnlyCollection<CaseDocument>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = documents.Values
            .Where(document => document.CaseId == caseId && document.Scope.Matches(scope))
            .OrderByDescending(document => document.UploadedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<CaseDocument>>(matches);
    }
}
