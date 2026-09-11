using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Policies;

/// <summary>
/// The insurer core-system (PAS) integration port (blueprint §14 Phase 7, WO 7.1) — looking up a
/// real policy from wherever the tenant's policy data actually lives, distinct from
/// IPolicyRepository, which is this platform's own storage (demo-seeded or manually onboarded
/// data). No PAS integration can be built generically — it needs a specific insurer's specific
/// API, credentials, and a data-sharing agreement, which is a partnership deliverable, not
/// something code alone can close. What code alone CAN do, and this port is it: define the seam
/// now, so a tenant moves from "no integration" to "an integration port with a configurable
/// adapter, awaiting a partner's endpoint" — see GenericRestPolicyLookupProvider (WO 7.2) — without
/// this platform's own call sites ever needing to change again once a real PAS is connected.
/// </summary>
public interface IPolicyLookupProvider
{
    Task<Policy?> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Policy>> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default);
}
