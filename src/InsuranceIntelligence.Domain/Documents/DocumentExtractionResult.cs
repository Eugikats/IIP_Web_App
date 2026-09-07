namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentExtractionResult(
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyDictionary<string, decimal> FieldConfidences,
    string ProviderName,
    DateTimeOffset ExtractedAt,
    bool Approved,
    string? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    IReadOnlyDictionary<string, DocumentFieldCorrection>? FieldCorrections = null,
    IReadOnlyDictionary<string, BoundingBox>? FieldBoundingBoxes = null,
    string? ModelVersion = null);
