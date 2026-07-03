using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken);
    Task UpsertAsync(Customer customer, CancellationToken cancellationToken);
}
