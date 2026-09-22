using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Communications;

/// <summary>
/// The three-step resolver from docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md §3.2,
/// applied to SMS. Registered as the one ISmsProvider the rest of the application ever sees:
///
///   1. An active, validated IntegrationConnection for this tenant → use it.
///   2. Otherwise, the deployment's environment-configured provider, if not Mock → use it.
///   3. Otherwise → ConfigurationRequired (honest, never a fake success).
///
/// This is what lets a tenant admin connect SMS from a settings screen with no redeploy, while
/// every existing environment-variable-only deployment keeps working unchanged.
///
/// Outcome M / AGENTS.md non-negotiable 5 ("no non-transactional message to a customer without a
/// recorded consent") is enforced here rather than in each real provider: every SMS this platform
/// sends, from any caller, resolves through this one class first, so this is the single narrowest
/// point that guarantees the rule without touching every call site individually. A LowRisk request
/// (a transactional status notice, see OutboundSmsGuard) is exempt, matching non-negotiable 4's own
/// carve-out for low-risk transactional sends; everything else needs a current, unwithdrawn
/// ConsentRecord for this recipient and purpose.
/// </summary>
public sealed class TenantAwareSmsProvider(
    IIntegrationConnectionRepository connectionRepository,
    IConnectionSecretProtector protector,
    ISmsProviderFactory providerFactory,
    ILogger<TenantAwareSmsProvider> logger,
    IConsentRepository? consentRepository = null) : ISmsProvider
{
    public async Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.LowRisk && consentRepository is not null)
        {
            var consent = await consentRepository.GetCurrentAsync(request.Scope, request.Recipient.Trim(), ConsentPurposes.Sms, cancellationToken);
            if (!ConsentEnforcementGuard.IsPermitted(consent))
            {
                return new CommunicationSendResult(
                    "ConsentRequired",
                    string.Empty,
                    "No recorded, unwithdrawn consent for SMS messages to this recipient.");
            }
        }

        var connection = await connectionRepository.GetAsync(request.Scope, ConnectionType.Sms, cancellationToken);
        if (connection is { IsActive: true })
        {
            var settings = ConnectionSettingsCodec.Decode(protector, connection);
            var provider = providerFactory.Create(connection.ProviderKey, settings);
            if (provider is not null)
            {
                return await provider.SendAsync(request, cancellationToken);
            }

            // A saved connection with an unrecognized provider key is a real misconfiguration
            // (e.g. a provider was removed from the platform after being connected), report it
            // plainly rather than silently falling back to the deployment default, which would be
            // surprising and could send through the wrong account.
            logger.LogWarning(
                "SMS connection for tenant {TenantId}/{WorkspaceId} references unrecognized provider {ProviderKey}.",
                request.Scope.TenantId, request.Scope.WorkspaceId, connection.ProviderKey);
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, "The configured SMS provider is not recognized.");
        }

        return await providerFactory.CreateFallback().SendAsync(request, cancellationToken);
    }
}
