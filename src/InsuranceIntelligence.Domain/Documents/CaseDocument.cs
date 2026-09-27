using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public sealed record CaseDocument
{
    public required Guid DocumentId { get; init; }
    public required Guid CaseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string ObjectKey { get; init; }
    public required DocumentType DocumentType { get; init; }

    /// <summary>Which document this actually is, as a catalog key (claim intake build prompt
    /// outcome A). Set from the customer's own choice when they uploaded it against a named slot
    /// (outcome B); falls back to <see cref="DocumentType"/>'s name when the customer had no choice
    /// to make (email and older WhatsApp uploads) or on a document stored before this field
    /// existed. Never required, so old JSON payloads with no such property deserialize cleanly.
    /// The customer's choice is what completeness matches against; <see cref="Classification"/>
    /// stays the classifier's own independent guess, so the two can be compared.</summary>
    public string? DocumentTypeKey { get; init; }

    public required DocumentProcessingStatus ProcessingStatus { get; init; }
    public required DocumentClassificationResult Classification { get; init; }
    public required DocumentExtractionResult Extraction { get; init; }
    public required IReadOnlyCollection<DocumentValidationWarning> ValidationWarnings { get; init; }
    public required string UploadedByUserId { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public required string CorrelationId { get; init; }
}
