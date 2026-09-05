using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record KnowledgeChunk
{
    public required string ChunkId { get; init; }
    public required Guid KnowledgeDocumentId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string DocumentName { get; init; }
    public required string ProductLine { get; init; }
    public required string Version { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Content { get; init; }
    public required KnowledgeSourceReference SourceReference { get; init; }
    /// <summary>Relevance score returned by Azure AI Search (0–1). Null when not available.</summary>
    public double? Score { get; init; }
    /// <summary>Section title from the source document, when returned by the search index.</summary>
    public string? Section { get; init; }
}
