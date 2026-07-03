using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

public interface ICustomerUpdateRepository
{
    Task SaveAsync(CustomerUpdateDraft update, CancellationToken cancellationToken = default);

    Task<CustomerUpdateDraft?> GetAsync(Guid updateId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerUpdateDraft>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerUpdateDraft>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
