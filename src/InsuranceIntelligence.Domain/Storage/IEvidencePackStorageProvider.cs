using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Storage;

public interface IEvidencePackStorageProvider
{
    Task<EvidencePackStorageResult> StoreAsync(EvidencePackStorageRequest request, CancellationToken cancellationToken = default);
}

public sealed record EvidencePackStorageRequest(
    TenantScope Scope,
    Guid? CaseId,
    string FileName,
    string ContentType,
    Stream Content);

public sealed record EvidencePackStorageResult(
    string ObjectKey,
    string ContentType,
    long FileSizeBytes);
