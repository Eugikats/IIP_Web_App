using System.Security.Cryptography;
using InsuranceIntelligence.Domain.Common;
using Microsoft.Extensions.Logging;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>An uploaded file as it was stored: where, its SHA-256, and where its separate preview
/// is (null when there is none).</summary>
public sealed record StoredOriginal(DocumentStorageResult Storage, string Sha256, string? PreviewObjectKey);

/// <summary>
/// Stores an uploaded file exactly as received and records what is needed to show it was never
/// altered (build-loop item 11): the original bytes go to storage unchanged (so a photo keeps its
/// EXIF), a SHA-256 of those bytes is kept, and a separate preview is made for looking at. A
/// preview that cannot be made never fails the upload; the original is what counts.
/// </summary>
public static class DocumentOriginals
{
    public const int PreviewMaxEdgePixels = 1600;
    public const string PreviewName = "preview.jpg";

    public static async Task<StoredOriginal> StoreAsync(
        IFormFile file,
        Guid documentId,
        Guid caseId,
        TenantScope scope,
        string contentType,
        IDocumentStorageProvider storageProvider,
        IImageDerivativeProvider? derivativeProvider,
        CancellationToken cancellationToken,
        ILoggerFactory? loggerFactory = null)
    {
        string sha256;
        await using (var hashing = file.OpenReadStream())
        {
            sha256 = Convert.ToHexString(await SHA256.HashDataAsync(hashing, cancellationToken)).ToLowerInvariant();
        }

        DocumentStorageResult storage;
        await using (var stream = file.OpenReadStream())
        {
            storage = await storageProvider.StoreAsync(
                new DocumentStorageRequest(documentId, caseId, scope, file.FileName, contentType, file.Length, stream),
                cancellationToken);
        }

        string? previewKey = null;
        if (derivativeProvider is not null)
        {
            try
            {
                await using var source = file.OpenReadStream();
                var preview = await derivativeProvider.CreatePreviewAsync(source, file.FileName, PreviewMaxEdgePixels, cancellationToken);
                if (preview is not null)
                {
                    await using var content = new MemoryStream(preview.Content);
                    previewKey = await storageProvider.StoreDerivativeAsync(scope, caseId, documentId, PreviewName, preview.ContentType, content, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A derivative is optional. Do not turn a stored original into a failed upload.
                loggerFactory?.CreateLogger(nameof(DocumentOriginals)).LogInformation(
                    "Image preview was not stored for document {DocumentId}; failure type {FailureType}.",
                    documentId,
                    exception.GetType().Name);
            }
        }

        return new StoredOriginal(storage, sha256, previewKey);
    }
}
