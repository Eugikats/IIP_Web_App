using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Distribution;

public sealed record Branch(
    string BranchId,
    string TenantId,
    string WorkspaceId,
    string Name,
    string Region);

public interface IBranchRepository
{
    Task<Branch?> GetAsync(string branchId, TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Branch>> ListAsync(TenantScope scope, CancellationToken cancellationToken);
    Task UpsertAsync(Branch branch, CancellationToken cancellationToken);
}
