using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Administration;

public interface ITenantConfigurationRepository
{
    Task<TenantConfiguration> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(TenantConfiguration configuration, CancellationToken cancellationToken = default);
}

public interface IUserAccountRepository
{
    Task<IReadOnlyCollection<InsuranceUserAccount>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<InsuranceUserAccount?> GetAsync(string userId, TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(InsuranceUserAccount user, CancellationToken cancellationToken = default);
}
