using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Policies;

/// <summary>
/// The same three-step resolution TenantAwareSmsProvider/TenantAwareEmailSendingProvider already
/// establish, applied to policy lookup, registered as the one IPolicyLookupProvider the rest of
/// the application ever sees:
///
///   1. An active PolicySystem Connection for this tenant → the configured provider adapter.
///   2. Otherwise → InternalPolicyLookupProvider (the workspace's own registered policy data).
///
/// Once a connection exists, failures are returned as explicit provider states. They never fall
/// back to the internal register, which could otherwise hide stale data behind an apparent success.
/// </summary>
public sealed class TenantAwarePolicyLookupProvider(
    IIntegrationConnectionRepository connectionRepository,
    IConnectionSecretProtector protector,
    InternalPolicyLookupProvider fallback,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IPolicyLookupProvider
{
    public async Task<PolicyLookupResult> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var provider = await ResolveAsync(scope, cancellationToken);
        return await provider.FindPolicyByNumberAsync(policyNumber, scope, cancellationToken);
    }

    public async Task<PolicyCollectionLookupResult> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var provider = await ResolveAsync(scope, cancellationToken);
        return await provider.FindPoliciesByCustomerAsync(customerId, scope, cancellationToken);
    }

    private async Task<IPolicyLookupProvider> ResolveAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var connection = await connectionRepository.GetAsync(scope, ConnectionType.PolicySystem, cancellationToken);
        if (connection is not { IsActive: true })
        {
            return fallback;
        }

        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        if (!string.Equals(connection.ProviderKey, "GenericRest", StringComparison.OrdinalIgnoreCase))
        {
            return new UnsupportedPolicyLookupProvider(connection.ProviderKey);
        }

        var restSettings = GenericRestPolicySettings.FromSettings(settings);
        return new GenericRestPolicyLookupProvider(
            restSettings,
            httpClientFactory.CreateClient(nameof(GenericRestPolicyLookupProvider)),
            loggerFactory.CreateLogger<GenericRestPolicyLookupProvider>());
    }
}

internal sealed class UnsupportedPolicyLookupProvider(string providerKey) : IPolicyLookupProvider
{
    public Task<PolicyLookupResult> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PolicyLookupResult(PolicyLookupStatus.UnsupportedProvider, null, providerKey));

    public Task<PolicyCollectionLookupResult> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PolicyCollectionLookupResult(PolicyLookupStatus.UnsupportedProvider, [], providerKey));
}
