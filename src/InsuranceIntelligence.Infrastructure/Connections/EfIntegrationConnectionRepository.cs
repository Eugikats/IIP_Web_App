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

    public Task SaveAsync(IntegrationConnection connection, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.IntegrationConnections,
            IntegrationConnection.StorageKey(connection.Scope, connection.ConnectionType),
            connection.Scope,
            Serialize(connection),
            cancellationToken: cancellationToken);

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
