using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Communications;

/// <summary>
/// The three-step resolution order (blueprint §3.2), applied to email, the exact structure of
/// TenantAwareSmsProvider: an active tenant IntegrationConnection first (SMTP), then the
/// deployment's environment-configured fallback (Graph, or Not configured), then honestly
/// reporting ConfigurationRequired. Registered as the one IEmailSendingProvider the rest of the
/// application sees.
/// </summary>
public sealed class TenantAwareEmailSendingProvider(
    IIntegrationConnectionRepository connectionRepository,
    IConnectionSecretProtector protector,
    IEmailSendingProviderFactory providerFactory,
    ILogger<TenantAwareEmailSendingProvider> logger) : IEmailSendingProvider
{
    public async Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
    {
        var connection = await connectionRepository.GetAsync(request.Scope, ConnectionType.Email, cancellationToken);
        if (connection is { IsActive: true })
        {
            var settings = ConnectionSettingsCodec.Decode(protector, connection);
            var provider = providerFactory.Create(connection.ProviderKey, settings);
            if (provider is not null)
            {
                return await provider.SendEmailAsync(request, cancellationToken);
            }

            logger.LogWarning(
                "Email connection for tenant {TenantId}/{WorkspaceId} references unrecognized provider {ProviderKey}.",
                request.Scope.TenantId, request.Scope.WorkspaceId, connection.ProviderKey);
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, "The configured email provider is not recognized.");
        }

        return await providerFactory.CreateFallback().SendEmailAsync(request, cancellationToken);
    }
}
