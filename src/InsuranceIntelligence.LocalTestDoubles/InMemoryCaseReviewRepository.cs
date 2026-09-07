using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.CaseReview;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryCaseReviewRepository : ICaseReviewRepository
{
    private readonly ConcurrentDictionary<Guid, CaseReviewSummary> reviews = new();

    public Task SaveAsync(CaseReviewSummary review, CancellationToken cancellationToken = default)
    {
        reviews[review.CaseReviewId] = review;
        return Task.CompletedTask;
    }

    public Task<CaseReviewSummary?> GetLatestForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var latest = reviews.Values
            .Where(review => review.CaseId == caseId && review.Scope.Matches(scope))
            .OrderByDescending(review => review.GeneratedAt)
            .FirstOrDefault();

        return Task.FromResult(latest);
    }

    public Task<IReadOnlyCollection<CaseReviewSummary>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = reviews.Values
            .Where(review => review.CaseId == caseId && review.Scope.Matches(scope))
            .OrderByDescending(review => review.GeneratedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<CaseReviewSummary>>(matches);
    }
}
