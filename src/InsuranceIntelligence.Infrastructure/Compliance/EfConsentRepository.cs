using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Compliance;

/// <summary>Append-only, same pattern as EfAuditRepository/EfCustomerConversationRepository,
/// every SaveAsync inserts a new row keyed by a fresh RecordId, so a withdrawal never erases the
/// grant that preceded it. CustomerIdentifier is the indexed column (matches ThreadId on
/// EfCustomerConversationRepository); Purpose has no dedicated index since one customer's row
/// count is always small, so filtering it in memory after the indexed customer lookup is the
/// right tradeoff over a second index column.</summary>
public sealed class EfConsentRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IConsentRepository
{
    public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.ConsentRecords,
            record.RecordId,
            record.Scope,
            Serialize(record),
            persisted => persisted.CustomerIdentifier = record.CustomerIdentifier,
            cancellationToken);

    public async Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ConsentRecords.AsNoTracking(), scope)
            .Where(record => record.CustomerIdentifier == customerIdentifier)
            .ToArrayAsync(cancellationToken);

        return records
            .Select(record => Deserialize<ConsentRecord>(record.PayloadJson))
            .Where(record => record.Purpose == purpose)
            .OrderByDescending(record => record.RecordedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ConsentRecords.AsNoTracking(), scope)
            .Where(record => record.CustomerIdentifier == customerIdentifier)
            .ToArrayAsync(cancellationToken);

        return records
            .Select(record => Deserialize<ConsentRecord>(record.PayloadJson))
            .OrderByDescending(record => record.RecordedAt)
            .ToArray();
    }
}
