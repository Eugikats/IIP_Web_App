using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public interface IDocumentStorageProvider
{
    Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default);

    Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The configured document storage provider does not support document reads.");

    Task<string> WriteNormalizedArtifactAsync(
        TenantScope scope,
        string objectKey,
        string artifactName,
        string normalizedJson,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The configured document storage provider does not support normalized artifact writes.");
}
