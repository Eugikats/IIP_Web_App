using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

public static partial class AzureProviderSupport
{
    public static TokenCredential Credential() => new DefaultAzureCredential();

    public static string SafeBlobName(TenantScope scope, params string[] segments) =>
        string.Join('/', new[] { scope.TenantId, scope.WorkspaceId }.Concat(segments.Select(SanitizeSegment)));

    public static BlobContainerClient BlobContainer(AzureBlobOptions options, string containerName)
    {
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
