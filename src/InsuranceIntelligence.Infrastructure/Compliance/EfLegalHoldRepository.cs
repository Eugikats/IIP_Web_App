using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Compliance;

public sealed class EfLegalHoldRepository(InsuranceIntelligenceDbContext dbContext) : JsonRepositoryBase(dbContext), ILegalHoldRepository
{
    public async Task<LegalHold?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = LegalHold.StorageKey(scope);
        var record = await Scoped(DbContext.LegalHolds.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<LegalHold>(record.PayloadJson);
    }

    public Task SaveAsync(LegalHold hold, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.LegalHolds, LegalHold.StorageKey(hold.Scope), hold.Scope, Serialize(hold), cancellationToken: cancellationToken);
}
