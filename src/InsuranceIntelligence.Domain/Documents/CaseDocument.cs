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
    public required DocumentProcessingStatus ProcessingStatus { get; init; }
    public required DocumentClassificationResult Classification { get; init; }
    public required DocumentExtractionResult Extraction { get; init; }
    public required IReadOnlyCollection<DocumentValidationWarning> ValidationWarnings { get; init; }
    public required string UploadedByUserId { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public required string CorrelationId { get; init; }
}
