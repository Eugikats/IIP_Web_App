using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken);
    Task UpsertAsync(Customer customer, CancellationToken cancellationToken);

    /// <summary>Customers with this phone number or email, found without reading the whole workspace when the store has an index (build-loop item 35).
    /// May return a few extra; the caller applies its own exact rule.</summary>
    async Task<IReadOnlyCollection<Customer>> FindByContactAsync(TenantScope scope, string? phone, string? email, CancellationToken cancellationToken)
    {
        var phoneKey = Common.LookupKeys.PhoneKey(phone);
        var emailKey = Common.LookupKeys.EmailKey(email);
        return phoneKey is null && emailKey is null
            ? []
            : (await ListAsync(scope, cancellationToken))
                .Where(customer => (phoneKey is not null && Common.LookupKeys.PhoneKey(customer.Phone) == phoneKey) || (emailKey is not null && Common.LookupKeys.EmailKey(customer.Email) == emailKey))
                .ToArray();
    }
}
