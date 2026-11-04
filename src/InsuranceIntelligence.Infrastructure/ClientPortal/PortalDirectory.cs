using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.ClientPortal;

public sealed class PersistedPortalIdentity : PersistedScopedJson;

public sealed class EfPortalIdentityRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IPortalIdentityRepository
{
    public async Task<PortalIdentity?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = PortalIdentity.StorageKey(scope);
        var record = await Scoped(DbContext.PortalIdentities.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<PortalIdentity>(record.PayloadJson);
    }

    public Task SaveAsync(PortalIdentity identity, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.PortalIdentities, PortalIdentity.StorageKey(identity.Scope), identity.Scope, Serialize(identity), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<PortalIdentity>> ListAllAsync(CancellationToken cancellationToken = default) =>
        (await DbContext.PortalIdentities.AsNoTracking().ToListAsync(cancellationToken))
            .Select(record => Deserialize<PortalIdentity>(record.PayloadJson))
            .ToArray();
}

/// <summary>
/// Finds a workspace from what a customer arrives with: the short name in their link, or the verified web address they opened
/// (build-loop item 29). It is a cached snapshot rather than a database read on every request, because these lookups are made
/// by unauthenticated callers: the snapshot is loaded once, refreshed when an administrator saves, after 30 seconds, and (at most
/// every 5 seconds) when an unknown name arrives, so a stranger trying random names cannot make it hammer the database.
/// </summary>
public interface IPortalDirectory
{
    PortalIdentity? BySlug(string slug);

    /// <summary>The workspace that owns this web address, only once the address is verified.</summary>
    PortalIdentity? ByHostname(string hostname);

    PortalIdentity? ForScope(TenantScope scope);

    PortalIdentity? BySmsSecretHash(string secretHash);

    /// <summary>Whether a browser page served from this origin (for example https://claims.insurer.com) belongs to a workspace.</summary>
    bool IsPortalOrigin(string origin);

    Task RefreshIfStaleAsync(CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Like <see cref="BySlug"/>, but first re-reads when the snapshot is old or the name is not known yet.</summary>
    Task<PortalIdentity?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<PortalIdentity?> FindByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
}

public sealed class PortalDirectory(IServiceScopeFactory scopeFactory, TimeSpan? missRefreshInterval = null) : IPortalDirectory
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MissRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly TimeSpan missInterval = missRefreshInterval ?? MissRefreshInterval;
    private volatile IReadOnlyList<PortalIdentity>? snapshot;
    private long lastLoadTicks;

    // A name that two workspaces hold (two administrators saving the same short name at the same moment) belongs to nobody: it
    // fails closed rather than sending a customer to the wrong insurer.
    private PortalIdentity? Only(Func<PortalIdentity, bool> matches)
    {
        var found = Loaded().Where(matches).Take(2).ToArray();
        return found.Length == 1 ? found[0] : null;
    }

    public PortalIdentity? BySlug(string slug) =>
        string.IsNullOrWhiteSpace(slug) ? null : Only(item => string.Equals(item.Slug, slug.Trim(), StringComparison.Ordinal));

    public PortalIdentity? ByHostname(string hostname) =>
        string.IsNullOrWhiteSpace(hostname)
            ? null
            : Only(item => item.HostnameIsVerified && string.Equals(item.Hostname, hostname.Trim().ToLowerInvariant(), StringComparison.Ordinal));

    public PortalIdentity? ForScope(TenantScope scope) => Loaded().FirstOrDefault(item => item.Scope.Matches(scope));

    public PortalIdentity? BySmsSecretHash(string secretHash) =>
        string.IsNullOrWhiteSpace(secretHash)
            ? null
            : Only(item => item.SmsWebhookSecretHash is not null && string.Equals(item.SmsWebhookSecretHash, secretHash, StringComparison.Ordinal));

    public bool IsPortalOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort)
        {
            return false;
        }

        return ByHostname(uri.Host) is not null;
    }

    public async Task<PortalIdentity?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        await RefreshIfStaleAsync(cancellationToken);
        var found = BySlug(slug);
        if (found is null && !string.IsNullOrWhiteSpace(slug) && SinceLastLoad() >= missInterval)
        {
            await RefreshAsync(cancellationToken);
            found = BySlug(slug);
        }

        return found;
    }

    public async Task<PortalIdentity?> FindByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        await RefreshIfStaleAsync(cancellationToken);
        var found = ByHostname(hostname);
        if (found is null && !string.IsNullOrWhiteSpace(hostname) && SinceLastLoad() >= missInterval)
        {
            await RefreshAsync(cancellationToken);
            found = ByHostname(hostname);
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
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetService<IPortalIdentityRepository>();
                snapshot = repository is null ? [] : await repository.ListAllAsync(cancellationToken);
                Interlocked.Exchange(ref lastLoadTicks, DateTimeOffset.UtcNow.UtcTicks);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // What was known stays in force (nothing, on the first read), and the read is retried shortly.
                using var scope = scopeFactory.CreateScope();
                scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<PortalDirectory>()
                    .LogWarning(exception, "The customer portal addresses could not be read; the earlier ones stay in force and the read is retried.");
                snapshot ??= [];
                Interlocked.Exchange(ref lastLoadTicks, (DateTimeOffset.UtcNow - MaxAge + missInterval).UtcTicks);
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private TimeSpan SinceLastLoad() => new(DateTimeOffset.UtcNow.UtcTicks - Interlocked.Read(ref lastLoadTicks));

    private IReadOnlyList<PortalIdentity> Loaded()
    {
        if (snapshot is null)
        {
            RefreshAsync().GetAwaiter().GetResult();
        }

        return snapshot!;
    }
}
