using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Translation;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Translation;

/// <summary>
/// The three-step resolver from docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md
/// §3.2, applied to translation, mirroring TenantAwareSmsProvider exactly:
///
///   1. An active, validated IntegrationConnection for this tenant (e.g. Sunbird AI) → use it.
///   2. Otherwise, the deployment's environment-configured provider (AfriSlm/Mock), if not
///      disabled → use it.
///   3. Otherwise → not configured (TranslationService falls back to English, never a fake success).
///
/// This is what lets a tenant admin connect Sunbird AI from /app/admin/connections with no
/// redeploy, while every existing environment-variable-only deployment keeps working unchanged.
/// Registered as the one ITranslationProvider/ITranslationEngineHealth the rest of the application
/// ever sees.
/// </summary>
public sealed class TenantAwareTranslationProvider(
    IIntegrationConnectionRepository connectionRepository,
    IConnectionSecretProtector protector,
    ITranslationProviderFactory providerFactory,
    ILogger<TenantAwareTranslationProvider> logger) : ITranslationProvider, ITranslationEngineHealth
{
    public async Task<string> TranslateAsync(
        string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(scope, cancellationToken);
        if (resolution.Provider is null)
        {
            // A saved connection with an unrecognized provider key is a real misconfiguration (e.g.
            // a provider was removed from the platform after being connected). This is reported
            // plainly rather than silently falling back to the deployment default, which would be
            // surprising and could translate through the wrong account, matching
            // TenantAwareSmsProvider's own rule for the same situation. TranslationService's caller
            // treats any exception from the provider as a fallback to English, never a crash.
            throw new InvalidOperationException("The configured translation provider is not recognized by this deployment.");
        }

        return await resolution.Provider.TranslateAsync(text, from, to, scope, cancellationToken);
    }

    public async Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(scope, cancellationToken);
        return resolution.Provider is not null && await resolution.Provider.IsConfiguredAsync(scope, cancellationToken);
    }

    public async Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(scope, cancellationToken);
        return resolution.Provider is not null
            ? await resolution.Provider.GetModelTagAsync(scope, cancellationToken)
            : "translation-misconfigured";
    }

    public async Task<TranslationEngineStatus> CheckAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(scope, cancellationToken);
        return resolution.Provider is ITranslationEngineHealth health
            ? await health.CheckAsync(scope, cancellationToken)
            : TranslationEngineStatus.NotConfigured;
    }

    private async Task<ProviderResolution> ResolveAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var connection = await connectionRepository.GetAsync(scope, ConnectionType.Translation, cancellationToken);
        if (connection is not { IsActive: true })
        {
            return new ProviderResolution(providerFactory.CreateFallback());
        }

        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        var provider = providerFactory.Create(connection.ProviderKey, settings);
        if (provider is not null)
        {
            return new ProviderResolution(provider);
        }

        logger.LogWarning(
            "Translation connection for tenant {TenantId}/{WorkspaceId} references unrecognized provider {ProviderKey}.",
            scope.TenantId, scope.WorkspaceId, connection.ProviderKey);
        return new ProviderResolution(null);
    }

    private readonly record struct ProviderResolution(ITranslationProvider? Provider);
}
