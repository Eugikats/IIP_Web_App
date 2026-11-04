using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Storage;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// Evidence pack storage on local disk for developer runtimes. Keys are prefixed
/// <c>{tenantId}/{workspaceId}/</c> exactly like the Azure Blob adapter, and a read refuses any key
/// outside the caller's own prefix, so tenant isolation is exercised locally too.
/// </summary>
public sealed partial class LocalMockEvidencePackStorageProvider(IWebHostEnvironment environment, IConfiguration configuration) : IEvidencePackStorageProvider
{
    public async Task<EvidencePackStorageResult> StoreAsync(EvidencePackStorageRequest request, CancellationToken cancellationToken = default)
    {
        var safeName = SafeFileName().Replace(Path.GetFileName(request.FileName), "_");
        var objectKey = string.Join('/', request.Scope.TenantId, request.Scope.WorkspaceId, "evidence-packs", $"{Guid.NewGuid():N}_{safeName}");
        var fullPath = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var output = File.Create(fullPath))
        {
            await request.Content.CopyToAsync(output, cancellationToken);
        }

        return new EvidencePackStorageResult(objectKey, request.ContentType, new FileInfo(fullPath).Length);
    }

    public Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default)
    {
        var expectedPrefix = $"{scope.TenantId}/{scope.WorkspaceId}/";
        if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal) || objectKey.Contains("..", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The stored object does not belong to this tenant workspace.");
        }

        Stream stream = File.OpenRead(ResolvePath(objectKey));
        return Task.FromResult(stream);
    }

    public Task<int> DeleteObjectAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default)
    {
        var expectedPrefix = $"{scope.TenantId}/{scope.WorkspaceId}/";
        if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal) || objectKey.Contains("..", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The stored object does not belong to this tenant workspace.");
        }

        var path = ResolvePath(objectKey);
        if (!File.Exists(path))
        {
            return Task.FromResult(0);
        }

        File.Delete(path);
        return Task.FromResult(1);
    }

    private string ResolvePath(string objectKey)
    {
        var root = configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH"]
            ?? Path.Combine(environment.ContentRootPath, ".local", "document-storage");
        return Path.Combine(root, objectKey.Replace('/', Path.DirectorySeparatorChar));
    }

    [GeneratedRegex("[^a-zA-Z0-9._-]")]
    private static partial Regex SafeFileName();
}
