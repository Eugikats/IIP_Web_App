using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Knowledge;

public sealed record KnowledgeDocumentResponse(
    Guid KnowledgeDocumentId,
    string DocumentName,
    string ProductLine,
    string DocumentType,
    string Version,
    DateOnly? EffectiveDate,
    DateOnly? ExpiryDate,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string ApprovalStatus,
    string IndexedStatus,
    string Sensitivity,
    string? GovernanceReason,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);

public sealed record KnowledgeDocumentDetailResponse(
    KnowledgeDocumentResponse Document,
    IReadOnlyCollection<KnowledgeChunkResponse> Chunks);

public sealed record KnowledgeChunkResponse(
    string ChunkId,
    int ChunkIndex,
    string Excerpt,
    string SourceLabel);

public sealed record KnowledgeGovernanceRequest(
    string? ActorUserId,
    string? GovernanceReason,
    string? Sensitivity = null);
