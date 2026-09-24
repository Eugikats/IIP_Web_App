using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>Fully resolved, decrypted WhatsApp credentials the inbound/outbound providers actually
/// use, whichever source they came from.</summary>
public sealed record WhatsAppEffectiveSettings(
    string PhoneNumberId,
    string AccessToken,
    string AppSecret,
    string VerifyToken,
    string GraphBaseUrl,
    int RequestTimeoutSeconds,
    string TenantId,
    string WorkspaceId,
    string? IntegrationUserId,
    string Source);

/// <summary>
/// Resolves the WhatsApp credentials the inbound/outbound providers use, preferring tenant-saved
/// configuration (entered through the settings screen, encrypted at rest) over the deployment's
/// static environment-variable configuration. This is a cached, hot-reloadable singleton rather
/// than a per-call DB lookup: IWhatsAppInboundProvider's IsConfigured/IsValidSignature/
/// TryVerifySubscription members are synchronous by contract (a webhook signature check has no
/// business awaiting a database round trip on every inbound Meta request), so the cache is loaded
/// once lazily and explicitly refreshed by WhatsAppConfigurationEndpoints immediately after a save
///, a viewer who just connected WhatsApp does not need to wait for a restart or a cache TTL.
/// </summary>
public interface IWhatsAppRuntimeSettingsProvider
{
    WhatsAppEffectiveSettings? Current { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class WhatsAppRuntimeSettingsProvider : IWhatsAppRuntimeSettingsProvider
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly InfrastructureOptions options;
    private readonly object gate = new();
    private volatile WhatsAppEffectiveSettings? cached;
    private volatile bool loaded;

    public WhatsAppRuntimeSettingsProvider(IServiceScopeFactory scopeFactory, InfrastructureOptions options)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
    }

    public WhatsAppEffectiveSettings? Current
    {
        get
        {
            if (!loaded)
            {
                // First access blocks briefly on a real DB read, acceptable here (happens once,
                // not per webhook call) and avoids making every interface member async for a value
                // that changes only when someone saves new settings.
                RefreshAsync().GetAwaiter().GetResult();
            }

            return cached;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetService<IWhatsAppChannelConfigurationRepository>();
        var protector = scope.ServiceProvider.GetService<IWhatsAppCredentialProtector>();

        WhatsAppEffectiveSettings? resolved = null;
        if (repository is not null && protector is not null
            && !string.IsNullOrWhiteSpace(options.TenantId) && !string.IsNullOrWhiteSpace(options.WorkspaceId))
        {
            var tenantScope = TenantScope.Create(options.TenantId!, options.WorkspaceId!);
            var saved = await repository.GetAsync(tenantScope, cancellationToken);
            if (saved is { IsActive: true })
            {
                resolved = new WhatsAppEffectiveSettings(
                    saved.PhoneNumberId,
                    protector.Unprotect(saved.EncryptedAccessToken),
                    protector.Unprotect(saved.EncryptedAppSecret),
                    protector.Unprotect(saved.EncryptedVerifyToken),
                    options.WhatsApp.GraphBaseUrl,
                    options.WhatsApp.RequestTimeoutSeconds,
                    options.TenantId!,
                    options.WorkspaceId!,
                    options.WhatsApp.IntegrationUserId,
                    "TenantConfigured");
            }
        }

        resolved ??= FromEnvironment();

        lock (gate)
        {
            cached = resolved;
            loaded = true;
        }
    }

    private WhatsAppEffectiveSettings? FromEnvironment()
    {
        var settings = options.WhatsApp;
        if (string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            || string.IsNullOrWhiteSpace(settings.AccessToken)
            || string.IsNullOrWhiteSpace(settings.AppSecret)
            || string.IsNullOrWhiteSpace(settings.VerifyToken)
            || string.IsNullOrWhiteSpace(settings.TenantId)
            || string.IsNullOrWhiteSpace(settings.WorkspaceId))
        {
            return null;
        }

        return new WhatsAppEffectiveSettings(
            settings.PhoneNumberId!,
            settings.AccessToken!,
            settings.AppSecret!,
            settings.VerifyToken!,
            settings.GraphBaseUrl,
            settings.RequestTimeoutSeconds,
            settings.TenantId!,
            settings.WorkspaceId!,
            settings.IntegrationUserId,
            "EnvironmentConfigured");
    }
}
