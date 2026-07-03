using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Policies;

public sealed record Coverage(
    string CoverageId,
    string TenantId,
    string WorkspaceId,
    string PolicyId,
    string Name,
    string Limit,
    string Deductible,
    string InsuredObject,
    string Description);

public interface ICoverageRepository
{
    Task<Coverage?> GetAsync(string coverageId, TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Coverage>> ListForPolicyAsync(string policyId, TenantScope scope, CancellationToken cancellationToken);
    Task UpsertAsync(Coverage coverage, CancellationToken cancellationToken);
}
