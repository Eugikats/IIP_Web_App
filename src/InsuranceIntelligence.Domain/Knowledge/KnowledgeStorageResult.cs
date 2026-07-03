namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record KnowledgeStorageResult(
    string ObjectKey,
    string ContentType,
    long FileSizeBytes,
    string ExtractedText);
