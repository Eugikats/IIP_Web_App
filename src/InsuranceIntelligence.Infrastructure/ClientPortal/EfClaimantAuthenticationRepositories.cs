using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.ClientPortal;

public sealed class EfClaimantMagicLinkTokenRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IClaimantMagicLinkTokenRepository
{
    public Task SaveAsync(ClaimantMagicLinkToken token, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.ClaimantMagicLinkTokens,
            token.TokenId,
            token.Scope,
            Serialize(token),
            record =>
            {
                record.CaseId = token.CaseId;
                record.TokenHash = token.TokenHash;
            },
            cancellationToken);

    public async Task<ClaimantMagicLinkToken?> GetByTokenHashAsync(string tokenHash, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.ClaimantMagicLinkTokens.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        return record is null ? null : Deserialize<ClaimantMagicLinkToken>(record.PayloadJson);
    }
}

public sealed class EfClaimantPortalSessionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IClaimantPortalSessionRepository
{
    public Task SaveAsync(ClaimantPortalSession session, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.ClaimantPortalSessions,
            session.SessionId,
            session.Scope,
            Serialize(session),
            record =>
            {
                record.CaseId = session.CaseId;
                record.SessionTokenHash = session.SessionTokenHash;
            },
            cancellationToken);

    public async Task<ClaimantPortalSession?> GetByTokenHashAsync(string tokenHash, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.ClaimantPortalSessions.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.SessionTokenHash == tokenHash, cancellationToken);
        return record is null ? null : Deserialize<ClaimantPortalSession>(record.PayloadJson);
    }
}
