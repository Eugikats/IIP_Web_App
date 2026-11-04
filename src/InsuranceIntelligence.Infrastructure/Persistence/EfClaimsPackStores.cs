using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>The history of claims packs emailed for a claim (build-loop item 25). Reads are limited to the workspace.</summary>
public sealed class EfClaimsPackDispatchRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IClaimsPackDispatchRepository
{
    public Task SaveAsync(ClaimsPackDispatch dispatch, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.ClaimsPackDispatches,
            dispatch.DispatchId,
            dispatch.Scope,
            Serialize(dispatch),
            record => record.CaseId = dispatch.CaseId,
            cancellationToken);

    public async Task<IReadOnlyList<ClaimsPackDispatch>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ClaimsPackDispatches.AsNoTracking(), scope).Where(item => item.CaseId == caseId).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<ClaimsPackDispatch>(record.PayloadJson)).OrderByDescending(item => item.At).ToArray();
    }
}

/// <summary>Download links for stored packs. Looked up by the fingerprint of the token alone, because the person
/// opening the link has no workspace or sign in; the token is the whole proof.</summary>
public sealed class EfClaimsPackShareLinkRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IClaimsPackShareLinkRepository
{
    public Task SaveAsync(ClaimsPackShareLink link, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.ClaimsPackShareLinks,
            link.LinkId,
            link.Scope,
            Serialize(link),
            record =>
            {
                record.CaseId = link.CaseId;
                record.TokenHash = link.TokenHash;
            },
            cancellationToken);

    public async Task<ClaimsPackShareLink?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.ClaimsPackShareLinks.AsNoTracking().FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        return record is null ? null : Deserialize<ClaimsPackShareLink>(record.PayloadJson);
    }
}
