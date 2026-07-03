using Azure.Storage.Blobs;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Providers;

public sealed class AzureProviderHealthCheck(InfrastructureOptions options) : IProviderHealthCheck
{
    public async Task<ProviderStatus> CheckAsync(string providerCode, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (!providerCode.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase)
            && !providerCode.Equals("BlobStorage", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderStatus(providerCode, "not_configured", "No real connectivity check is registered for this provider.", checkedAt);
        }

        if (!options.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderStatus(providerCode, "not_configured", "Azure Blob Storage provider is not configured.", checkedAt);
        }

        if (string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString)
            && string.IsNullOrWhiteSpace(options.AzureBlob.Endpoint)
            && string.IsNullOrWhiteSpace(options.AzureBlob.AccountName))
        {
            return new ProviderStatus(providerCode, "missing", "Azure Blob Storage endpoint, account name, or connection string is missing.", checkedAt);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.ProviderHealth.TimeoutSeconds)));

        try
        {
            var container = CreateContainerClient(options.AzureBlob);
            await container.ExistsAsync(timeout.Token);
            return new ProviderStatus(providerCode, "ready", "Azure Blob Storage responded to a container metadata check.", checkedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderStatus(providerCode, "unhealthy", "Azure Blob Storage health check timed out.", checkedAt);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(providerCode, "unhealthy", $"Azure Blob Storage health check failed: {ex.GetType().Name}.", checkedAt);
        }
    }

    private static BlobContainerClient CreateContainerClient(AzureBlobOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new BlobContainerClient(options.ConnectionString, options.DocumentContainer);
        }

        var endpoint = !string.IsNullOrWhiteSpace(options.Endpoint)
            ? options.Endpoint
            : $"https://{options.AccountName}.blob.core.windows.net";

        return new BlobContainerClient(new Uri($"{endpoint.TrimEnd('/')}/{options.DocumentContainer}"), new Azure.Identity.DefaultAzureCredential());
    }
}
