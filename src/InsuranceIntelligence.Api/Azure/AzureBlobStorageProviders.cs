using System.Text;
using Azure.Storage.Blobs.Models;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

public sealed class AzureBlobDocumentStorageProvider(InfrastructureOptions options, ILogger<AzureBlobDocumentStorageProvider> logger) : IDocumentStorageProvider
{
    public async Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default)
    {
        var container = AzureProviderSupport.BlobContainer(options.AzureBlob, options.AzureBlob.DocumentContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var objectKey = AzureProviderSupport.SafeBlobName(
            request.Scope,
            "cases",
            request.CaseId.ToString(),
            "documents",
            $"{request.DocumentId}_{request.OriginalFileName}");
        var blob = container.GetBlobClient(objectKey);
        var headers = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType };

        logger.LogInformation("Uploading claim document to Azure Blob. Tenant={TenantId} Workspace={WorkspaceId} CaseId={CaseId} DocumentId={DocumentId}",
            request.Scope.TenantId, request.Scope.WorkspaceId, request.CaseId, request.DocumentId);
        await blob.UploadAsync(request.Content, headers, cancellationToken: cancellationToken);
        return new DocumentStorageResult(objectKey, headers.ContentType, request.FileSizeBytes);
    }

    public async Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureScopedReference(scope, objectKey);
        var container = AzureProviderSupport.BlobContainer(options.AzureBlob, options.AzureBlob.DocumentContainer);
        var blob = container.GetBlobClient(objectKey);

        logger.LogInformation("Reading claim document from Azure Blob. Tenant={TenantId} Workspace={WorkspaceId}",
            scope.TenantId, scope.WorkspaceId);
        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task<string> WriteNormalizedArtifactAsync(
        TenantScope scope,
        string objectKey,
        string artifactName,
        string normalizedJson,
        CancellationToken cancellationToken = default)
    {
        EnsureScopedReference(scope, objectKey);
        var container = AzureProviderSupport.BlobContainer(options.AzureBlob, options.AzureBlob.DocumentContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var artifactKey = AzureProviderSupport.SafeBlobName(
            scope,
            "cases",
            "artifacts",
            Path.GetFileNameWithoutExtension(objectKey),
            artifactName);
        var blob = container.GetBlobClient(artifactKey);

        logger.LogInformation("Writing normalized document artifact to Azure Blob. Tenant={TenantId} Workspace={WorkspaceId}",
            scope.TenantId, scope.WorkspaceId);
        await blob.UploadAsync(
            BinaryData.FromString(normalizedJson),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
        return artifactKey;
    }

    private static void EnsureScopedReference(TenantScope scope, string objectKey)
    {
        var expectedPrefix = $"{scope.TenantId}/{scope.WorkspaceId}/";
        if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Document storage reference is outside the requested tenant workspace.");
        }
    }
}

public sealed class AzureBlobKnowledgeStorageProvider(InfrastructureOptions options, ILogger<AzureBlobKnowledgeStorageProvider> logger) : IKnowledgeStorageProvider
{
    public async Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        memory.Position = 0;

        var container = AzureProviderSupport.BlobContainer(options.AzureBlob, options.AzureBlob.KnowledgeContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var objectKey = AzureProviderSupport.SafeBlobName(request.Scope, "knowledge", $"{request.KnowledgeDocumentId}_{request.OriginalFileName}");
        var blob = container.GetBlobClient(objectKey);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType;

        logger.LogInformation("Uploading knowledge document to Azure Blob. Tenant={TenantId} Workspace={WorkspaceId} KnowledgeDocumentId={KnowledgeDocumentId}",
            request.Scope.TenantId, request.Scope.WorkspaceId, request.KnowledgeDocumentId);
        await blob.UploadAsync(memory, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

        return new KnowledgeStorageResult(objectKey, contentType, request.FileSizeBytes, ExtractText(request.OriginalFileName, bytes));
    }

    private static string ExtractText(string fileName, byte[] bytes)
    {
        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return $"Azure Blob knowledge document reference for {Path.GetFileName(fileName)}. Text extraction should be handled by the configured document processing pipeline before production retrieval.";
    }
}

public sealed class AzureBlobEvidencePackStorageProvider(InfrastructureOptions options, ILogger<AzureBlobEvidencePackStorageProvider> logger) : IEvidencePackStorageProvider
{
    public async Task<EvidencePackStorageResult> StoreAsync(EvidencePackStorageRequest request, CancellationToken cancellationToken = default)
    {
        var container = AzureProviderSupport.BlobContainer(options.AzureBlob, options.AzureBlob.EvidenceContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var caseSegment = request.CaseId?.ToString() ?? "tenant";
        var objectKey = AzureProviderSupport.SafeBlobName(request.Scope, "evidence-packs", caseSegment, request.FileName);
        var blob = container.GetBlobClient(objectKey);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType;

        using var bufferedContent = new MemoryStream();
        await request.Content.CopyToAsync(bufferedContent, cancellationToken);
        var contentLength = bufferedContent.Length;
        bufferedContent.Position = 0;

        logger.LogInformation("Uploading evidence pack to Azure Blob. Tenant={TenantId} Workspace={WorkspaceId} CaseId={CaseId}",
            request.Scope.TenantId, request.Scope.WorkspaceId, request.CaseId);
        await blob.UploadAsync(bufferedContent, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);
        return new EvidencePackStorageResult(objectKey, contentType, contentLength);
    }

    public async Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureScopedReference(scope, objectKey);
        var container = AzureProviderSupport.BlobContainer(options.AzureBlob, options.AzureBlob.EvidenceContainer);
        var blob = container.GetBlobClient(objectKey);

        logger.LogInformation("Reading evidence pack from Azure Blob. Tenant={TenantId} Workspace={WorkspaceId}",
            scope.TenantId, scope.WorkspaceId);
        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    private static void EnsureScopedReference(TenantScope scope, string objectKey)
    {
        var expectedPrefix = $"{scope.TenantId}/{scope.WorkspaceId}/";
        if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Evidence pack storage reference is outside the requested tenant workspace.");
        }
    }
}
