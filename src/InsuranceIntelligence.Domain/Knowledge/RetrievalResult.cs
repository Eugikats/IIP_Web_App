namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record RetrievalResult(
    string Query,
    string? ProductLine,
    IReadOnlyCollection<KnowledgeChunk> Chunks,
    IReadOnlyCollection<SourceCitation> Citations);
