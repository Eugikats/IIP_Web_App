using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Connections;

public sealed class EfIntegrationConnectionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IIntegrationConnectionRepository
{
    public async Task<IntegrationConnection?> GetAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default)
    {
        var id = IntegrationConnection.StorageKey(scope, connectionType);
        var record = await Scoped(DbContext.IntegrationConnections.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<IntegrationConnection>(record.PayloadJson);
    }

    public async Task<IntegrationConnection?> FindByExternalAccountAsync(
        ConnectionType connectionType,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return null;
        var record = await DbContext.IntegrationConnections.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ConnectionType == connectionType.ToString()
                && item.ExternalAccountId == accountId, cancellationToken);
        if (record is null) return null;
        var connection = Deserialize<IntegrationConnection>(record.PayloadJson);
        return connection.IsActive ? connection : null;
    }

    public Task SaveAsync(IntegrationConnection connection, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.IntegrationConnections,
            IntegrationConnection.StorageKey(connection.Scope, connection.ConnectionType),
            connection.Scope,
            Serialize(connection),
            record =>
            {
                record.ConnectionType = connection.ConnectionType.ToString();
                record.ExternalAccountId = ExtractExternalAccountId(connection);
            },
            cancellationToken: cancellationToken);

    private static string? ExtractExternalAccountId(IntegrationConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.LastValidationDiscoveredJson)) return null;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(connection.LastValidationDiscoveredJson);
            return json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && json.RootElement.TryGetProperty("accountId", out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public async Task DeleteAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default)
    {
        var id = IntegrationConnection.StorageKey(scope, connectionType);
        var record = await Scoped(DbContext.IntegrationConnections, scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is not null)
        {
            DbContext.IntegrationConnections.Remove(record);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
