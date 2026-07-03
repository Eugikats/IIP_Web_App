using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentStorageRequest(
    Guid DocumentId,
    Guid CaseId,
    TenantScope Scope,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content);
