using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Connectors;

public sealed class EfDataConnectorRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IDataConnectorRepository
{
    public async Task<DataConnector?> GetAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.DataConnectors.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == connectorId, cancellationToken);
        return record is null ? null : Deserialize<DataConnector>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<DataConnector>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.DataConnectors.AsNoTracking(), scope).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<DataConnector>(record.PayloadJson)).OrderBy(connector => connector.CreatedAt).ToArray();
    }

    /// <summary>
    /// The only read in this repository that crosses workspaces, for the scheduled sync. Each connector it returns
    /// carries its own scope, and the sync does all of its work under that scope, never a caller's.
    /// </summary>
    public async Task<IReadOnlyList<DataConnector>> ListActiveAcrossWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var records = await DbContext.DataConnectors.AsNoTracking().ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<DataConnector>(record.PayloadJson)).Where(connector => connector.Status == ConnectorStatus.Active).ToArray();
    }

    public Task SaveAsync(DataConnector connector, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.DataConnectors, connector.ConnectorId, connector.Scope, Serialize(connector with { UpdatedAt = DateTimeOffset.UtcNow }), cancellationToken: cancellationToken);

    public async Task DeleteAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.DataConnectors, scope).FirstOrDefaultAsync(item => item.Id == connectorId, cancellationToken);
        if (record is not null)
        {
            DbContext.DataConnectors.Remove(record);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class EfConnectorRunRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IConnectorRunRepository
{
    public Task SaveAsync(ConnectorRun run, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.ConnectorRuns, run.RunId, run.Scope, Serialize(run), record => record.ConnectorId = run.ConnectorId, cancellationToken);

    public async Task<IReadOnlyList<ConnectorRun>> ListAsync(TenantScope scope, Guid connectorId, int limit, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ConnectorRuns.AsNoTracking(), scope)
            .Where(record => record.ConnectorId == connectorId)
            .OrderByDescending(record => record.UpdatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<ConnectorRun>(record.PayloadJson)).ToArray();
    }
}

public sealed class EfConnectorItemRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IConnectorItemRepository
{
    public async Task<ConnectorItem?> GetAsync(TenantScope scope, Guid connectorId, string externalId, CancellationToken cancellationToken = default)
    {
        var id = ConnectorItem.StorageKey(connectorId, externalId);
        var record = await Scoped(DbContext.ConnectorItems.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<ConnectorItem>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<ConnectorItem>> ListAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ConnectorItems.AsNoTracking(), scope)
            .Where(record => record.ConnectorId == connectorId)
            .ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<ConnectorItem>(record.PayloadJson)).ToArray();
    }

    public Task SaveAsync(ConnectorItem item, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.ConnectorItems, item.ItemId, item.Scope, Serialize(item), record => record.ConnectorId = item.ConnectorId, cancellationToken);

    public async Task DeleteForConnectorAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ConnectorItems, scope).Where(record => record.ConnectorId == connectorId).ToListAsync(cancellationToken);
        DbContext.ConnectorItems.RemoveRange(records);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
