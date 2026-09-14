using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Storage;

public interface IEvidencePackStorageProvider
{
    Task<EvidencePackStorageResult> StoreAsync(EvidencePackStorageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads back a previously exported evidence pack (blueprint §14 Phase 6, WO 6.2),
    /// mirrors IDocumentStorageProvider.ReadAsync's shape. Without this, StoreAsync's ObjectKey
    /// was write-only: a real single-document export existed, but nothing could ever retrieve it
    /// again after the initial upload.</summary>
    Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default);
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
