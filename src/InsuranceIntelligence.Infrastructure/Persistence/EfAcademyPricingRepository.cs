using InsuranceIntelligence.Domain.Academy;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>One record per workspace holding the prices of its IIP Academy courses.</summary>
public sealed class EfAcademyPricingRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAcademyPricingRepository
{
    public async Task<AcademyPricing?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AcademyPricings.AsNoTracking(), scope).FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : Deserialize<AcademyPricing>(record.PayloadJson);
    }

    public Task SaveAsync(AcademyPricing pricing, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.AcademyPricings, DeterministicId(pricing.Scope), pricing.Scope, Serialize(pricing), cancellationToken: cancellationToken);

    private static Guid DeterministicId(TenantScope scope) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"academy-pricing:{scope.TenantId}:{scope.WorkspaceId}")));
}
