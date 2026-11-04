using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Cases;

public static class CaseLookups
{
    /// <summary>
    /// The claims that could belong to one customer: any on these policy numbers, or with this phone number or email. The result can hold
    /// a few that turn out not to match, so the caller applies its own exact rule. Replaces reading every claim in the workspace.
    /// </summary>
    public static async Task<IReadOnlyCollection<ClaimCase>> ListRelatedAsync(
        this ICaseRepository repository, TenantScope scope, IReadOnlyCollection<string> policyNumbers, string? phone, string? email, CancellationToken cancellationToken = default)
    {
        var byPolicy = policyNumbers.Count == 0 ? [] : await repository.ListByPolicyNumbersAsync(scope, policyNumbers, cancellationToken);
        var byContact = await repository.ListByContactAsync(scope, phone, email, cancellationToken);
        return byPolicy.Concat(byContact).GroupBy(claim => claim.CaseId).Select(group => group.First()).ToArray();
    }

    /// <summary>The statuses in which a claim still needs work.</summary>
    public static readonly CaseStatus[] OpenStatuses =
    [
        CaseStatus.New, CaseStatus.Assigned, CaseStatus.InReview, CaseStatus.MissingDocuments,
        CaseStatus.ReadyForReview, CaseStatus.PendingApproval, CaseStatus.ReadyForExport, CaseStatus.Escalated,
    ];
}
