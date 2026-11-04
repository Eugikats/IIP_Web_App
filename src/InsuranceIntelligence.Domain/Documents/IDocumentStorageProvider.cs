using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public interface IDocumentStorageProvider
{
    Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default);

    Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The configured document storage provider does not support document reads.");

    /// <summary>Stores something made from a document (a preview, a PDF built from photos) as its own
    /// object next to the original and returns its key. The original is never replaced.</summary>
    Task<string> StoreDerivativeAsync(
        TenantScope scope,
        Guid caseId,
        Guid documentId,
        string name,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The configured document storage provider does not support derived copies.");

    Task<string> WriteNormalizedArtifactAsync(
        TenantScope scope,
        string objectKey,
        string artifactName,
        string normalizedJson,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The configured document storage provider does not support normalized artifact writes.");
}
