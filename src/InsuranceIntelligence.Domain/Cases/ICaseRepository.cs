using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Cases;

public interface ICaseRepository
{
    Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default);

    Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    // The lookups below find claims without reading the whole workspace (build-loop item 35). A store with an index overrides them; the
    // defaults read everything and filter, so a simple store or a test double stays correct. Each may return a few extra claims, never
    // fewer than match, so the caller still applies its own exact rule to the result.

    /// <summary>Claims on any of these policy numbers (compared ignoring case).</summary>
    async Task<IReadOnlyCollection<ClaimCase>> ListByPolicyNumbersAsync(TenantScope scope, IReadOnlyCollection<string> policyNumbers, CancellationToken cancellationToken = default)
    {
        var keys = policyNumbers.Select(LookupKeys.PolicyKey).Where(key => key is not null).ToHashSet();
        return keys.Count == 0 ? [] : (await ListAsync(scope, cancellationToken)).Where(claim => keys.Contains(LookupKeys.PolicyKey(claim.PolicyNumber))).ToArray();
    }

    /// <summary>Claims whose customer has this phone number or email.</summary>
    async Task<IReadOnlyCollection<ClaimCase>> ListByContactAsync(TenantScope scope, string? phone, string? email, CancellationToken cancellationToken = default)
    {
        var phoneKey = LookupKeys.PhoneKey(phone);
        var emailKey = LookupKeys.EmailKey(email);
        return phoneKey is null && emailKey is null
            ? []
            : (await ListAsync(scope, cancellationToken))
                .Where(claim => (phoneKey is not null && LookupKeys.PhoneKey(claim.CustomerPhone) == phoneKey) || (emailKey is not null && LookupKeys.EmailKey(claim.CustomerEmail) == emailKey))
                .ToArray();
    }

    /// <summary>Claims in any of these statuses.</summary>
    async Task<IReadOnlyCollection<ClaimCase>> ListByStatusesAsync(TenantScope scope, IReadOnlyCollection<CaseStatus> statuses, CancellationToken cancellationToken = default) =>
        (await ListAsync(scope, cancellationToken)).Where(claim => statuses.Contains(claim.Status)).ToArray();

    /// <summary>Claims routed to this team.</summary>
    async Task<IReadOnlyCollection<ClaimCase>> ListByTeamAsync(TenantScope scope, string team, CancellationToken cancellationToken = default) =>
        (await ListAsync(scope, cancellationToken)).Where(claim => string.Equals(claim.AssignedTeam, team, StringComparison.Ordinal)).ToArray();
}
