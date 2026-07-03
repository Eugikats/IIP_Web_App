using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Policies;

public interface IPolicyRepository
{
    Task<Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken);
    Task UpsertAsync(Policy policy, CancellationToken cancellationToken);
}
