using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Connections;

public sealed class EfUserFileConnectionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IUserFileConnectionRepository
{
    public async Task<UserFileConnection?> GetAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default)
    {
        var id = UserFileConnection.StorageKey(scope, userId, provider);
        var record = await Scoped(DbContext.UserFileConnections.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<UserFileConnection>(record.PayloadJson);
    }

    public Task SaveAsync(UserFileConnection connection, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.UserFileConnections,
            UserFileConnection.StorageKey(connection.Scope, connection.UserId, connection.Provider),
            connection.Scope,
            Serialize(connection),
            cancellationToken: cancellationToken);

    public async Task DeleteAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default)
    {
        var id = UserFileConnection.StorageKey(scope, userId, provider);
        var record = await Scoped(DbContext.UserFileConnections, scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is not null)
        {
            DbContext.UserFileConnections.Remove(record);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
