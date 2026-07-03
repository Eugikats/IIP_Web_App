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
        var storageRoot = configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH"]
            ?? Path.Combine(environment.ContentRootPath, ".local", "document-storage");
        var fullPath = Path.Combine(storageRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var output = File.Create(fullPath);
        await request.Content.CopyToAsync(output, cancellationToken);

        return new DocumentStorageResult(objectKey, request.ContentType, request.FileSizeBytes);
    }

    [GeneratedRegex("[^a-zA-Z0-9._-]")]
    private static partial Regex SafeFileNamePattern();
}
