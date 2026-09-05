namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentClassificationResult(
    DocumentType DocumentType,
    decimal ConfidenceScore,
    string ProviderName,
    DateTimeOffset ClassifiedAt,
    string? ModelVersion = null);
