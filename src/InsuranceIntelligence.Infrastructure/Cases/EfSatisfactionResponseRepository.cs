using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Cases;

/// <summary>Append-only, same pattern as EfConsentRepository: a case can only ever be asked the
/// satisfaction question once in the normal flow, but this stays append-only rather than
/// single-row so a correction never silently overwrites what was actually recorded.</summary>
public sealed class EfSatisfactionResponseRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ISatisfactionResponseRepository
{
    public Task SaveAsync(SatisfactionResponse response, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.SatisfactionResponses,
            response.ResponseId,
            response.Scope,
            Serialize(response),
            persisted => persisted.CaseId = response.CaseId,
            cancellationToken);

    public async Task<IReadOnlyCollection<SatisfactionResponse>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.SatisfactionResponses.AsNoTracking(), scope)
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return records
            .Select(item => Deserialize<SatisfactionResponse>(item.PayloadJson))
            .OrderByDescending(item => item.RecordedAt)
            .ToArray();
    }
}
