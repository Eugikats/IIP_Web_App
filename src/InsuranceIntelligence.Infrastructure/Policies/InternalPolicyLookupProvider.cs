using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Infrastructure.Policies;

/// <summary>Looks policies up from this platform's own storage, demo-seeded data, or whatever a
/// tenant has manually onboarded, rather than a real insurer core system. The default
/// IPolicyLookupProvider until a tenant configures a real PolicySystem Connection (WO 7.2); see
/// TenantAwarePolicyLookupProvider for the resolution order between the two.</summary>
public sealed class InternalPolicyLookupProvider(IPolicyRepository policyRepository) : IPolicyLookupProvider
{
    public async Task<PolicyLookupResult> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var policy = await policyRepository.GetByPolicyNumberAsync(policyNumber, scope, cancellationToken);
        return new(policy is null ? PolicyLookupStatus.NotFound : PolicyLookupStatus.Found, policy, "IIPPolicyRegister");
    }

    public async Task<PolicyCollectionLookupResult> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var policies = await policyRepository.ListByCustomerAsync(customerId, scope, cancellationToken);
        return new(policies.Count == 0 ? PolicyLookupStatus.NotFound : PolicyLookupStatus.Found, policies, "IIPPolicyRegister");
    }
}
