using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class ProviderHealthCheckEf(
    InsuranceIntelligenceDbContext dbContext,
    InfrastructureOptions options) : IProviderHealthCheck
{
    private readonly AzureProviderHealthCheck _azureProviderHealthCheck = new(options);

    public async Task<ProviderStatus> CheckAsync(string providerCode, CancellationToken cancellationToken = default)
    {
        if (IsSqlProvider(providerCode))
        {
            return await CheckSqlServerAsync(providerCode, cancellationToken);
        }

        return await _azureProviderHealthCheck.CheckAsync(providerCode, cancellationToken);
    }

    private async Task<ProviderStatus> CheckSqlServerAsync(string providerCode, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (!options.StorageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderStatus(providerCode, "not_configured", "SQL Server storage provider is not configured.", checkedAt);
        }

        if (string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
        {
            return new ProviderStatus(providerCode, "missing", "SQL Server connection string is missing.", checkedAt);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.ProviderHealth.TimeoutSeconds)));

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
            return canConnect
                ? new ProviderStatus(providerCode, "ready", "SQL Server responded to a connectivity check.", checkedAt)
                : new ProviderStatus(providerCode, "unhealthy", "SQL Server connectivity check failed.", checkedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderStatus(providerCode, "unhealthy", "SQL Server health check timed out.", checkedAt);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(providerCode, "unhealthy", $"SQL Server health check failed: {ex.GetType().Name}.", checkedAt);
        }
    }

    private static bool IsSqlProvider(string providerCode) =>
        providerCode.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("AzureSql", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("AzureSQL", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("sql_server", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("database", StringComparison.OrdinalIgnoreCase)
        || providerCode.Equals("persistence", StringComparison.OrdinalIgnoreCase);
}
