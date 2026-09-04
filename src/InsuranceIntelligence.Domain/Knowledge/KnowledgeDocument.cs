using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record KnowledgeDocument
{
    public required Guid KnowledgeDocumentId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string DocumentName { get; init; }
    public required string ProductLine { get; init; }
    public required KnowledgeDocumentType DocumentType { get; init; }
    public required string Version { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string ObjectKey { get; init; }
    public required KnowledgeDocumentStatus ApprovalStatus { get; init; }
    public required KnowledgeIndexedStatus IndexedStatus { get; init; }
    // WO-17: unclassified by default — never retrieval-eligible until a human explicitly
    // classifies it as part of approval (KnowledgeEndpoints.ApproveKnowledgeDocument). Fail closed.
    public KnowledgeSensitivity Sensitivity { get; init; } = KnowledgeSensitivity.Unknown;
    public string? GovernanceReason { get; init; }
    public required IReadOnlyCollection<KnowledgeChunk> Chunks { get; init; }
    public required string UploadedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CorrelationId { get; init; }

    public bool IsActiveOn(DateOnly date) =>
        ApprovalStatus == KnowledgeDocumentStatus.Approved
        && IndexedStatus == KnowledgeIndexedStatus.Indexed
        && Sensitivity != KnowledgeSensitivity.Unknown
        && (EffectiveDate is null || EffectiveDate.Value <= date)
        && (ExpiryDate is null || ExpiryDate.Value >= date);
}
