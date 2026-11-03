using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Policies;

/// <summary>
/// Resolves policy records from the workspace register or a connected insurer policy system.
/// Results preserve provider health states so a missing policy is distinct from an unconfigured,
/// unauthorized, unavailable, unsupported, or malformed integration. Provider schemas are mapped
/// at the infrastructure edge; they never become the domain contract.
/// </summary>
public interface IPolicyLookupProvider
{
    Task<PolicyLookupResult> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default);

    Task<PolicyCollectionLookupResult> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default);
}

public enum PolicyLookupStatus
{
    Found,
    NotFound,
    NotConfigured,
    Unauthorized,
    Unavailable,
    Malformed,
    ProviderRejected,
    UnsupportedProvider
}

public sealed record PolicyLookupResult(PolicyLookupStatus Status, Policy? Policy, string Source);

public sealed record PolicyCollectionLookupResult(PolicyLookupStatus Status, IReadOnlyCollection<Policy> Policies, string Source);
