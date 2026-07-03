using Azure.Storage.Blobs;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Providers;

public sealed class AzureProviderHealthCheck(InfrastructureOptions options) : IProviderHealthCheck
{
    public async Task<ProviderStatus> CheckAsync(string providerCode, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (IsAzureOpenAi(providerCode))
        {
            return CheckAzureOpenAi(providerCode, checkedAt);
        }

        if (IsAzureAiSearch(providerCode))
        {
            return CheckAzureAiSearch(providerCode, checkedAt);
        }

        if (IsAzureBlob(providerCode))
        {
            return await CheckAzureBlobAsync(providerCode, checkedAt, cancellationToken);
        }

        return new ProviderStatus(providerCode, "not_configured", "No real connectivity check is registered for this provider.", checkedAt);
    }

    private async Task<ProviderStatus> CheckAzureBlobAsync(
        string providerCode,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
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

    private ProviderStatus CheckAzureOpenAi(string providerCode, DateTimeOffset checkedAt)
    {
        if (!options.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderStatus(providerCode, "not_configured", "Azure OpenAI provider is not configured.", checkedAt);
        }

        var missing = Missing(
            ("AzureOpenAI:Endpoint", options.AzureOpenAI.Endpoint),
            ("AzureOpenAI:ChatDeployment", options.AzureOpenAI.ChatDeployment));

        if (missing.Count > 0)
        {
            return new ProviderStatus(
                providerCode,
                "missing",
                $"{string.Join(" and ", missing)} must be configured.",
                checkedAt);
        }

        return new ProviderStatus(providerCode, "ready", "Azure OpenAI provider configuration is present.", checkedAt);
    }

    private ProviderStatus CheckAzureAiSearch(string providerCode, DateTimeOffset checkedAt)
    {
        if (!options.RetrievalProvider.Equals("AzureAiSearch", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderStatus(providerCode, "not_configured", "Azure AI Search provider is not configured.", checkedAt);
        }

        var missing = Missing(
            ("AzureAiSearch:Endpoint", options.AzureAiSearch.Endpoint),
            ("AzureAiSearch:IndexName", options.AzureAiSearch.IndexName));

        if (missing.Count > 0)
        {
            return new ProviderStatus(
                providerCode,
                "missing",
                $"{string.Join(" and ", missing)} must be configured.",
                checkedAt);
        }

        return new ProviderStatus(providerCode, "ready", "Azure AI Search provider configuration is present.", checkedAt);
    }

    private static bool IsAzureBlob(string providerCode) =>
        providerCode.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("BlobStorage", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("azure_blob", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("blob_storage", StringComparison.OrdinalIgnoreCase);

    private static bool IsAzureOpenAi(string providerCode) =>
        providerCode.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("AzureOpenAi", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("azure_openai", StringComparison.OrdinalIgnoreCase);

    private static bool IsAzureAiSearch(string providerCode) =>
        providerCode.Equals("AzureAiSearch", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("AzureAISearch", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("azure_ai_search", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Missing(params (string Name, string? Value)[] settings) =>
        settings
            .Where(setting => string.IsNullOrWhiteSpace(setting.Value))
            .Select(setting => setting.Name)
            .ToArray();

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
