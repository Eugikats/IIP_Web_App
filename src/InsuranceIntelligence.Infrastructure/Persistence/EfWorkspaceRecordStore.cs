using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// The workspace's own small records, in one table. The record id is derived from the organisation, the workspace, the
/// kind and the key, and every query filters on the organisation and workspace, so one workspace can neither read nor
/// overwrite another's. Reads treat a table that does not exist yet as empty; writes do not.
/// </summary>
public sealed class EfWorkspaceRecordStore(InsuranceIntelligenceDbContext dbContext) : IWorkspaceRecordStore
{
    private static Guid IdOf(TenantScope scope, string kind, string key) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"workspace:{scope.TenantId}:{scope.WorkspaceId}:{kind}:{key}")));

    private IQueryable<WorkspaceRecordEntity> Owned(TenantScope scope, string kind) =>
        dbContext.WorkspaceRecords.AsNoTracking()
            .Where(item => item.TenantId == scope.TenantId && item.WorkspaceId == scope.WorkspaceId && item.Kind == kind);

    private static WorkspaceRecord ToRecord(WorkspaceRecordEntity entity) => new(entity.Kind, entity.Key, entity.PayloadJson, entity.UpdatedAt);

    public async Task<WorkspaceRecord?> GetAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await Owned(scope, kind).FirstOrDefaultAsync(item => item.Key == key, cancellationToken);
            return entity is null ? null : ToRecord(entity);
        }
        catch (SqlException failure) when (failure.Number == 208)
        {
            return null;
        }
    }

    public async Task SaveAsync(TenantScope scope, string kind, string key, string payloadJson, CancellationToken cancellationToken = default)
    {
        var id = IdOf(scope, kind, key);
        for (var attempt = 0; ; attempt++)
        {
            var entity = await dbContext.WorkspaceRecords.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            var created = entity is null;
            if (entity is null)
            {
                entity = new WorkspaceRecordEntity
                {
                    Id = id,
                    TenantId = scope.TenantId,
                    WorkspaceId = scope.WorkspaceId,
                    Kind = kind,
                    Key = key,
                    PayloadJson = payloadJson,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                dbContext.WorkspaceRecords.Add(entity);
            }
            else if (entity.TenantId != scope.TenantId || entity.WorkspaceId != scope.WorkspaceId)
            {
                // The id is derived from the scope, so this cannot happen unless the data was tampered with.
                throw new TenantIsolationException();
            }
            else
            {
                entity.PayloadJson = payloadJson;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception failure) when (created && attempt < 2 && failure is DbUpdateException or ArgumentException or InvalidOperationException)
            {
                dbContext.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    public async Task<IReadOnlyList<WorkspaceRecord>> ListAsync(TenantScope scope, string kind, int take = 500, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await Owned(scope, kind)
                .OrderByDescending(item => item.UpdatedAt)
                .Take(Math.Clamp(take, 1, 5000))
                .ToListAsync(cancellationToken);
            return rows.Select(ToRecord).ToArray();
        }
        catch (SqlException failure) when (failure.Number == 208)
        {
            return [];
        }
    }

    public async Task DeleteAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default)
    {
        var id = IdOf(scope, kind, key);
        var entity = await dbContext.WorkspaceRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == scope.TenantId && item.WorkspaceId == scope.WorkspaceId, cancellationToken);
        if (entity is not null)
        {
            dbContext.WorkspaceRecords.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> CountAsync(TenantScope scope, string kind, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Owned(scope, kind).CountAsync(cancellationToken);
        }
        catch (SqlException failure) when (failure.Number == 208)
        {
            return 0;
        }
    }

    public async Task<IReadOnlyList<(TenantScope Scope, WorkspaceRecord Record)>> ListPlatformAsync(string kind, int take = 500, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.WorkspaceRecords.AsNoTracking()
            .Where(item => item.Kind == kind)
            .OrderByDescending(item => item.UpdatedAt)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken);
        return rows.Select(item => (
            TenantScope.Create(item.TenantId, item.WorkspaceId),
            ToRecord(item))).ToArray();
    }
}
