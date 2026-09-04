using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryKnowledgeIndexProvider : IKnowledgeIndexProvider
{
    private readonly ConcurrentDictionary<Guid, KnowledgeDocument> indexedDocuments = new();

    public Task IndexAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        if (document.ApprovalStatus == KnowledgeDocumentStatus.Approved
            && document.Sensitivity != KnowledgeSensitivity.Unknown)
        {
            indexedDocuments[document.KnowledgeDocumentId] = document with { IndexedStatus = KnowledgeIndexedStatus.Indexed };
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        indexedDocuments.TryRemove(knowledgeDocumentId, out _);
        return Task.CompletedTask;
    }

    public Task RebuildAsync(TenantScope scope, IReadOnlyCollection<KnowledgeDocument> approvedDocuments, CancellationToken cancellationToken = default)
    {
        foreach (var documentId in indexedDocuments.Values
                     .Where(document => document.Scope.Matches(scope))
                     .Select(document => document.KnowledgeDocumentId)
                     .ToArray())
        {
            indexedDocuments.TryRemove(documentId, out _);
        }

        foreach (var document in approvedDocuments.Where(document =>
            document.Scope.Matches(scope)
            && document.ApprovalStatus == KnowledgeDocumentStatus.Approved
            && document.Sensitivity != KnowledgeSensitivity.Unknown))
        {
            indexedDocuments[document.KnowledgeDocumentId] = document with { IndexedStatus = KnowledgeIndexedStatus.Indexed };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<KnowledgeChunk>> SearchAsync(TenantScope scope, string query, string? productLine, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var terms = Tokenize(query);
        var matches = indexedDocuments.Values
            .Where(document => document.Scope.Matches(scope))
            .Where(document => document.IsActiveOn(today))
            .Where(document => string.IsNullOrWhiteSpace(productLine) || string.Equals(document.ProductLine, productLine, StringComparison.OrdinalIgnoreCase))
            .SelectMany(document => document.Chunks)
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = Score(chunk.Content, terms)
            })
            .Where(match => match.Score > 0 || terms.Count == 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Chunk.ChunkIndex)
            .Take(5)
            .Select(match => match.Chunk)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<KnowledgeChunk>>(matches);
    }

    private static HashSet<string> Tokenize(string value) =>
        value.Split([' ', '.', ',', '?', '!', ';', ':', '/', '\\', '-', '_', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 2)
            .ToHashSet();

    private static int Score(string content, HashSet<string> terms)
    {
        if (terms.Count == 0)
        {
            return 1;
        }

        var normalized = content.ToLowerInvariant();
        return terms.Count(term => normalized.Contains(term, StringComparison.Ordinal));
    }
}
