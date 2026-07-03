using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Cases;

public interface ICaseRepository
{
    Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default);

    Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
