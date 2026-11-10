using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Fraud;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>The fraud agent's reviews of a claim, newest last. Every read is limited to the caller's workspace.</summary>
public sealed class EfFraudAgentReviewRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IFraudAgentReviewRepository
{
    public Task SaveAsync(FraudAgentReview review, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.FraudAgentReviews,
            review.ReviewId,
            review.Scope,
            Serialize(review),
            record =>
            {
                record.CaseId = review.CaseId;
                record.GeneratedAt = review.GeneratedAt;
            },
            cancellationToken);

    public async Task<FraudAgentReview?> GetLatestAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.FraudAgentReviews.AsNoTracking(), scope)
            .Where(item => item.CaseId == caseId)
            .ToListAsync(cancellationToken);
        var latest = records.OrderByDescending(item => item.GeneratedAt).FirstOrDefault();
        return latest is null ? null : Deserialize<FraudAgentReview>(latest.PayloadJson);
    }
}
