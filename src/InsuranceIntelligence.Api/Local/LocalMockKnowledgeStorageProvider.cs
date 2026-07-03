using System.Text;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Local;

public sealed partial class LocalMockKnowledgeStorageProvider(IWebHostEnvironment environment, IConfiguration configuration) : IKnowledgeStorageProvider
{
    public async Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var safeFileName = SafeFileNamePattern().Replace(Path.GetFileName(request.OriginalFileName), "_");
        var objectKey = string.Join(
            '/',
            request.Scope.TenantId,
            request.Scope.WorkspaceId,
            "knowledge",
            $"{request.KnowledgeDocumentId}_{safeFileName}");
        var storageRoot = configuration["INSURANCEINTELLIGENCE_LOCAL_KNOWLEDGE_STORAGE_PATH"]
            ?? Path.Combine(environment.ContentRootPath, ".local", "knowledge-storage");
        var fullPath = Path.Combine(storageRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

        return new KnowledgeStorageResult(
            objectKey,
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            request.FileSizeBytes,
            ExtractText(request.OriginalFileName, bytes));
    }

    private static string ExtractText(string fileName, byte[] bytes)
    {
        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return $"Mock indexed content for {Path.GetFileName(fileName)}. This uploaded knowledge document is available as approved policy guidance after approval.";
    }

    [GeneratedRegex("[^a-zA-Z0-9._-]")]
    private static partial Regex SafeFileNamePattern();
}
