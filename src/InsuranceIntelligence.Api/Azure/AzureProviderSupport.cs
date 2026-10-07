using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

public static partial class AzureProviderSupport
{
    // One credential for the whole process. A credential caches its tokens, so a new one per client
    // meant a fresh managed identity token request on most API calls and model calls: measured on
    // dev on 2026-10-07 at 3 to 6 seconds each, the bulk of a slow /api/v1/me.
    private static readonly Lazy<TokenCredential> SharedCredential = new(() => new DefaultAzureCredential());

    public static TokenCredential Credential() => SharedCredential.Value;

    public static string SafeBlobName(TenantScope scope, params string[] segments) =>
        string.Join('/', new[] { scope.TenantId, scope.WorkspaceId }.Concat(segments.Select(SanitizeSegment)));

    public static BlobContainerClient BlobContainer(AzureBlobOptions options, string containerName)
    {
        // Prefer managed identity. Connection-string clients are Development/local only and must
        // never win when UseManagedIdentity is true (protected runtimes fail closed at startup).
        if (!string.IsNullOrWhiteSpace(options.ConnectionString) && !options.UseManagedIdentity)
        {
            return new BlobContainerClient(options.ConnectionString, containerName);
        }

        var endpoint = BlobEndpoint(options);
        var containerUri = new Uri($"{endpoint}/{containerName}");
        return new BlobContainerClient(containerUri, Credential());
    }

    public static string BlobEndpoint(AzureBlobOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return options.Endpoint.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(options.AccountName))
        {
            return $"https://{options.AccountName}.blob.core.windows.net";
        }

        throw new InvalidOperationException("Azure Blob is selected but AzureBlob:Endpoint or AzureBlob:AccountName is not configured.");
    }

    public static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);

    public static string SafeError(string provider, Exception exception) =>
        $"{provider} operation failed. Check provider configuration, identity, network access, and service availability.";

    private static string SanitizeSegment(string segment) =>
        SafeSegmentPattern().Replace(Path.GetFileName(segment), "_");

    [GeneratedRegex("[^a-zA-Z0-9._=-]")]
    private static partial Regex SafeSegmentPattern();
}
