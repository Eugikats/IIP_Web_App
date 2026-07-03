using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record KnowledgeStorageRequest(
    Guid KnowledgeDocumentId,
    TenantScope Scope,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content);
