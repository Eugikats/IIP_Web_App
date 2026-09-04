using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Fraud;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Fraud;

public sealed class EfFraudSignalDispositionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IFraudSignalDispositionRepository
{
    public Task SaveAsync(FraudSignalDisposition disposition, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.FraudSignalDispositions,
            disposition.SignalId,
            disposition.Scope,
            Serialize(disposition),
            record => record.Status = disposition.Status.ToString(),
            cancellationToken);

    public async Task<FraudSignalDisposition?> GetAsync(Guid signalId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.FraudSignalDispositions.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == signalId, cancellationToken);
        return record is null ? null : Deserialize<FraudSignalDisposition>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<FraudSignalDisposition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.FraudSignalDispositions.AsNoTracking(), scope)
            .Select(record => Deserialize<FraudSignalDisposition>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}
