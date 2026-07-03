namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record KnowledgeSourceReference(
    Guid KnowledgeDocumentId,
    string ChunkId,
    string DocumentName,
    string ProductLine,
    string Version,
    string ObjectKey);
