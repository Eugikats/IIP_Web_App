using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
/// Resolves the WhatsApp credentials the inbound/outbound providers use. Every workspace that has
/// connected a number has its own saved connection (encrypted at rest), and an inbound webhook is
/// routed to the workspace whose connection holds the phone number it names (build-loop item 27).
/// A deployment that configures one number through environment variables is one more connection in
/// the same list, used for a workspace that has saved none of its own.
///
/// This is a cached, hot-reloadable singleton rather than a per-call DB lookup:
/// IWhatsAppInboundProvider's IsConfigured/TryVerifySubscription members are synchronous by
/// contract (a webhook check has no business awaiting a database round trip on every inbound Meta
/// request), so the snapshot is loaded once lazily, refreshed by WhatsAppConfigurationEndpoints
/// immediately after a save, and re-read when it is older than <see cref="MaxAge"/> or when a number
/// it has never seen arrives (a connection saved on another instance), never more than once every
/// <see cref="MissRefreshInterval"/> so a stranger posting random numbers cannot make it hammer the
/// database.
/// </summary>
public interface IWhatsAppRuntimeSettingsProvider
{
    IReadOnlyList<WhatsAppEffectiveSettings> All => Current is { } current ? [current] : [];
    bool AnyConfigured => HasAny;
    /// <summary>The connection of a deployment that has exactly one. Null when there is none or more
    /// than one: with several workspaces on a deployment there is no single answer, and anything that
    /// must act for a workspace asks <see cref="ForScope"/> instead.</summary>
    WhatsAppEffectiveSettings? Current { get; }

    /// <summary>Whether any workspace on this deployment has a live connection.</summary>
    bool HasAny => Current is not null;

    /// <summary>The workspace's own connection, or null when it has none (or it was switched off).</summary>
    WhatsAppEffectiveSettings? ForScope(TenantScope scope) => Current is { } current
        && current.TenantId == scope.TenantId && current.WorkspaceId == scope.WorkspaceId ? current : null;

    WhatsAppEffectiveSettings? ForTenant(TenantScope scope) => ForScope(scope);

    /// <summary>The connection that owns this business phone number. Null when no workspace has it, and
    /// also when more than one does (a misconfiguration that must fail closed rather than guess).</summary>
    WhatsAppEffectiveSettings? ForPhoneNumber(string phoneNumberId) =>
        All.SingleOrDefault(item => string.Equals(item.PhoneNumberId, phoneNumberId, StringComparison.Ordinal));

    /// <summary>The connection whose verify token this is. Meta's subscription check names no phone number,
    /// only the token this platform generated when the workspace connected.</summary>
    WhatsAppEffectiveSettings? ForVerifyToken(string verifyToken) =>
        All.SingleOrDefault(item => string.Equals(item.VerifyToken, verifyToken, StringComparison.Ordinal));

    /// <summary>Like <see cref="ForPhoneNumber"/>, but first re-reads the connections when they are old or
    /// the number is not known yet.</summary>
    Task<WhatsAppEffectiveSettings?> FindByPhoneNumberAsync(string phoneNumberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ForPhoneNumber(phoneNumberId));

    /// <summary>Re-reads the connections when they are older than the allowed age.</summary>
    Task RefreshIfStaleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class WhatsAppRuntimeSettingsProvider : IWhatsAppRuntimeSettingsProvider
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MissRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly InfrastructureOptions options;
    private readonly TimeSpan missRefreshInterval;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private volatile Snapshot? snapshot;
    private long lastLoadTicks;

    public WhatsAppRuntimeSettingsProvider(IServiceScopeFactory scopeFactory, InfrastructureOptions options, TimeSpan? missRefreshInterval = null)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.missRefreshInterval = missRefreshInterval ?? MissRefreshInterval;
    }

    private sealed record Snapshot(IReadOnlyList<WhatsAppEffectiveSettings> All);

    public IReadOnlyList<WhatsAppEffectiveSettings> All => Loaded().All;

    public WhatsAppEffectiveSettings? Current
    {
        get
        {
            var all = Loaded().All;
            return all.Count == 1 ? all[0] : null;
        }
    }

    public bool HasAny => Loaded().All.Count > 0;

    public WhatsAppEffectiveSettings? ForScope(TenantScope scope) =>
        Loaded().All.FirstOrDefault(item =>
            string.Equals(item.TenantId, scope.TenantId, StringComparison.Ordinal)
            && string.Equals(item.WorkspaceId, scope.WorkspaceId, StringComparison.Ordinal));

    public WhatsAppEffectiveSettings? ForTenant(TenantScope scope) => ForScope(scope);

    public WhatsAppEffectiveSettings? ForPhoneNumber(string phoneNumberId)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return null;
        }

        var owners = Loaded().All.Where(item => string.Equals(item.PhoneNumberId, phoneNumberId, StringComparison.Ordinal)).ToArray();
        return owners.Length == 1 ? owners[0] : null;
    }

    public WhatsAppEffectiveSettings? ForVerifyToken(string verifyToken)
    {
        if (string.IsNullOrEmpty(verifyToken))
        {
            return null;
        }

        var wanted = System.Text.Encoding.UTF8.GetBytes(verifyToken);
        WhatsAppEffectiveSettings? found = null;
        // Every connection is compared, in constant time, so the answer does not leak which token was close.
        foreach (var item in Loaded().All)
        {
            var candidate = System.Text.Encoding.UTF8.GetBytes(item.VerifyToken);
            if (System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(candidate, wanted))
            {
                found = item;
            }
        }

        return found;
    }

    public async Task<WhatsAppEffectiveSettings?> FindByPhoneNumberAsync(string phoneNumberId, CancellationToken cancellationToken = default)
    {
        await RefreshIfStaleAsync(cancellationToken);
        var found = ForPhoneNumber(phoneNumberId);
        if (found is not null || string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return found;
        }

        if (SinceLastLoad() >= missRefreshInterval)
        {
            await RefreshAsync(cancellationToken);
            found = ForPhoneNumber(phoneNumberId);
        }

        return found;
    }

    public async Task RefreshIfStaleAsync(CancellationToken cancellationToken = default)
    {
        if (snapshot is null || SinceLastLoad() >= MaxAge)
        {
            await RefreshAsync(cancellationToken);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                snapshot = await LoadAsync(cancellationToken);
                Interlocked.Exchange(ref lastLoadTicks, DateTimeOffset.UtcNow.UtcTicks);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The store could not be read. What was known stays in force (or, on the first read, nothing is
                // connected, which is honestly "not configured" and is retried shortly), rather than every call that
                // merely asks whether WhatsApp is connected throwing.
                using var scope = scopeFactory.CreateScope();
                scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<WhatsAppRuntimeSettingsProvider>()
                    .LogWarning(exception, "The WhatsApp connections could not be read; the earlier ones stay in force and the read is retried.");
                snapshot ??= new Snapshot([]);
                Interlocked.Exchange(ref lastLoadTicks, (DateTimeOffset.UtcNow - MaxAge + missRefreshInterval).UtcTicks);
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private TimeSpan SinceLastLoad() =>
        new(DateTimeOffset.UtcNow.UtcTicks - Interlocked.Read(ref lastLoadTicks));

    private Snapshot Loaded()
    {
        if (snapshot is null)
        {
            // First access blocks briefly on a real DB read; it happens once, not per webhook call, and
            // avoids making every interface member async for a value that changes only on a save.
            RefreshAsync().GetAwaiter().GetResult();
        }

        return snapshot!;
    }

    private async Task<Snapshot> LoadAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetService<IWhatsAppChannelConfigurationRepository>();
        var protector = scope.ServiceProvider.GetService<IWhatsAppCredentialProtector>();

        var all = new List<WhatsAppEffectiveSettings>();
        if (repository is not null && protector is not null)
        {
            foreach (var saved in await repository.ListActiveAsync(cancellationToken))
            {
                all.Add(new WhatsAppEffectiveSettings(
                    saved.PhoneNumberId,
                    protector.Unprotect(saved.EncryptedAccessToken),
                    protector.Unprotect(saved.EncryptedAppSecret),
                    protector.Unprotect(saved.EncryptedVerifyToken),
                    options.WhatsApp.GraphBaseUrl,
                    options.WhatsApp.RequestTimeoutSeconds,
                    saved.Scope.TenantId,
                    saved.Scope.WorkspaceId,
                    options.WhatsApp.IntegrationUserId,
                    "TenantConfigured"));
            }
        }

        var fromEnvironment = FromEnvironment();
        if (fromEnvironment is not null
            && !all.Any(item => item.TenantId == fromEnvironment.TenantId && item.WorkspaceId == fromEnvironment.WorkspaceId))
        {
            all.Add(fromEnvironment);
        }

        return new Snapshot(all);
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

/// <summary>
/// Whether the WhatsApp channel is actually live right now: either the deployment itself selected
/// WhatsAppCloudApi, or a tenant validated and saved a connection from the settings screen. The
/// two call sites this backs (webhook message/media ingestion, attachment download) previously
/// checked only the deployment's env var, so a tenant-saved connection could show "Verified" and
/// register its webhook successfully, while every real message Meta then delivered was still
/// rejected "ConfigurationRequired" (found live 2026-09-25: the GET verification passed, every
/// POST 503'd). That contradicted the documented design of this registration (see
/// InfrastructureRegistration.cs's comment: the real providers are registered unconditionally so a
/// validated connection is live without a deployment change) and the health endpoint's own,
/// separately-fixed check.
/// </summary>
public static class WhatsAppChannelProviderSupport
{
    public static bool IsLive(InfrastructureOptions options, WhatsAppEffectiveSettings? settings) =>
        options.ChannelProvider.Equals("WhatsAppCloudApi", StringComparison.OrdinalIgnoreCase)
        || string.Equals(settings?.Source, "TenantConfigured", StringComparison.Ordinal);
}
