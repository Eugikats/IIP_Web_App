using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Policies;

public interface IPolicyRepository
{
    Task<Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Looks a policy up by the human-readable number a claimant or officer actually has,
    /// distinct from <see cref="Policy.PolicyId"/>, which is an internal identifier nobody types.
    /// Case-insensitive: policy numbers are read off paperwork, not copy-pasted.
    /// </summary>
    Task<Policy?> GetByPolicyNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken);
    Task UpsertAsync(Policy policy, CancellationToken cancellationToken);
}
