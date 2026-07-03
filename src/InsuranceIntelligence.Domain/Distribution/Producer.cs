using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Distribution;

public sealed record Producer(
    string ProducerId,
    string TenantId,
    string WorkspaceId,
    string BranchId,
    string Name,
    string ProducerType);

public interface IProducerRepository
{
    Task<Producer?> GetAsync(string producerId, TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Producer>> ListAsync(TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Producer>> ListByBranchAsync(string branchId, TenantScope scope, CancellationToken cancellationToken);
    Task UpsertAsync(Producer producer, CancellationToken cancellationToken);
}
