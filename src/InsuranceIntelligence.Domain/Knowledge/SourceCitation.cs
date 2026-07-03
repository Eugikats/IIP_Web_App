namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record SourceCitation(
    Guid KnowledgeDocumentId,
    string ChunkId,
    string DocumentName,
    string ProductLine,
    string Version,
    string Excerpt,
    string Label);
