using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Local;

public sealed partial class LocalMockDocumentStorageProvider(IWebHostEnvironment environment, IConfiguration configuration) : IDocumentStorageProvider
{
    public async Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default)
    {
        var safeFileName = SafeFileNamePattern().Replace(Path.GetFileName(request.OriginalFileName), "_");
        var objectKey = string.Join(
            '/',
            request.Scope.TenantId,
            request.Scope.WorkspaceId,
            request.CaseId,
            $"{request.DocumentId}_{safeFileName}");
        var fullPath = ResolvePath(request.Scope, objectKey);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var output = File.Create(fullPath);
        await request.Content.CopyToAsync(output, cancellationToken);

        return new DocumentStorageResult(objectKey, request.ContentType, request.FileSizeBytes);
    }

    public async Task<string> StoreDerivativeAsync(
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        Guid caseId,
        Guid documentId,
        string name,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeName = SafeFileNamePattern().Replace(Path.GetFileName(name), "_");
        var objectKey = string.Join('/', scope.TenantId, scope.WorkspaceId, caseId, "derived", $"{documentId}_{safeName}");
        var storageRoot = configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH"]
            ?? Path.Combine(environment.ContentRootPath, ".local", "document-storage");
        var fullPath = Path.Combine(storageRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = File.Create(fullPath);
        await content.CopyToAsync(output, cancellationToken);
        return objectKey;
    }

    public Task<Stream> ReadAsync(InsuranceIntelligence.Domain.Common.TenantScope scope, string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolvePath(scope, objectKey);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    private string ResolvePath(InsuranceIntelligence.Domain.Common.TenantScope scope, string objectKey)
    {
        var segments = objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4
            || !string.Equals(segments[0], scope.TenantId, StringComparison.Ordinal)
            || !string.Equals(segments[1], scope.WorkspaceId, StringComparison.Ordinal)
            || segments.Any(segment => segment is "." or ".." || !SafePathSegmentPattern().IsMatch(segment)))
        {
            throw new UnauthorizedAccessException("The document storage key is outside the resolved tenant workspace.");
        }

        var storageRoot = configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH"]
            ?? Path.Combine(environment.ContentRootPath, ".local", "document-storage");
        var fullRoot = Path.GetFullPath(storageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, Path.Combine(segments)));
        var rootPrefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The document storage key is outside the local document storage directory.");
        }

        return fullPath;
    }

    [GeneratedRegex("[^a-zA-Z0-9._-]")]
    private static partial Regex SafeFileNamePattern();

    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex SafePathSegmentPattern();
}
