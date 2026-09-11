using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Policies;

/// <summary>
/// The same three-step resolution TenantAwareSmsProvider/TenantAwareEmailSendingProvider already
/// establish, applied to policy lookup — registered as the one IPolicyLookupProvider the rest of
/// the application ever sees:
///
///   1. An active PolicySystem Connection for this tenant → the generic REST adapter (WO 7.2).
///   2. Otherwise → InternalPolicyLookupProvider (this platform's own data, demo-seeded or
///      manually onboarded).
///
/// Unlike SMS/email, there is no third "ConfigurationRequired" state: InternalPolicyLookupProvider
/// is always a meaningful, real answer — every tenant has policy data in this platform's own
/// storage, whether or not a PAS partner is connected — so falling back to it is never dishonest
/// the way silently no-op'ing an unconfigured SMS send would be.
/// </summary>
public sealed class TenantAwarePolicyLookupProvider(
    IIntegrationConnectionRepository connectionRepository,
    IConnectionSecretProtector protector,
    InternalPolicyLookupProvider fallback,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IPolicyLookupProvider
{
    public async Task<Policy?> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var provider = await ResolveAsync(scope, cancellationToken);
        return await provider.FindPolicyByNumberAsync(policyNumber, scope, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Policy>> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
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
            return fallback; // Unrecognized provider key — fall back rather than fail policy lookup entirely.
        }

        var restSettings = GenericRestPolicySettings.FromSettings(settings);
        return new GenericRestPolicyLookupProvider(
            restSettings,
            httpClientFactory.CreateClient(nameof(GenericRestPolicyLookupProvider)),
            loggerFactory.CreateLogger<GenericRestPolicyLookupProvider>());
    }
}
