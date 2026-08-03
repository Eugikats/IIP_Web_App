using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Contestability;

public interface IClaimReviewRequestRepository
{
    Task SaveAsync(ClaimReviewRequest request, CancellationToken cancellationToken = default);

    Task<ClaimReviewRequest?> GetAsync(Guid requestId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ClaimReviewRequest>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
