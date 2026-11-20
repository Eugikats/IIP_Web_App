using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

// Storage for the organisation platform (department workspaces, automations, runs, the Intelligence Inbox, intents and
// settings). Every record is a JSON payload in a tenant and IIP-workspace scoped row, like the rest of IIP. A few columns
// are copied out of the payload only so they can be indexed or made unique. Services read a bounded list and filter in memory.

public sealed class PersistedOrgWorkspace : PersistedScopedJson
{
    public bool IsPrimary { get; set; }
}

public sealed class PersistedPlatformAutomation : PersistedScopedJson
{
    public Guid OrgWorkspaceId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class PersistedPlatformRun : PersistedScopedJson
{
    public Guid AutomationId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}

public sealed class PersistedPlatformInboxItem : PersistedScopedJson
{
    public string DedupeKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class PersistedPlatformIntent : PersistedScopedJson;

public sealed class PersistedPlatformSettings : PersistedScopedJson;

public abstract class EfPlatformStoreBase<TRecord, TRow>(InsuranceIntelligenceDbContext db) : JsonRepositoryBase(db), IPlatformStore<TRecord>
    where TRecord : class, IPlatformRecord
    where TRow : PersistedScopedJson, new()
{
    protected abstract DbSet<TRow> Rows { get; }

    /// <summary>Copies the indexed columns out of the record.</summary>
    protected virtual void Index(TRow row, TRecord record)
    {
    }

    public async Task<IReadOnlyList<TRecord>> ListAsync(TenantScope scope, int limit = 1000, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(Rows.AsNoTracking(), scope)
            .OrderByDescending(row => row.UpdatedAt)
            .Take(Math.Clamp(limit, 1, 5000))
            .ToListAsync(cancellationToken);
        return rows.Select(row => Deserialize<TRecord>(row.PayloadJson)).ToList();
    }

    public async Task<TRecord?> GetAsync(TenantScope scope, Guid id, CancellationToken cancellationToken = default)
    {
        var row = await Scoped(Rows.AsNoTracking(), scope).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return row is null ? null : Deserialize<TRecord>(row.PayloadJson);
    }

    public Task SaveAsync(TenantScope scope, TRecord record, CancellationToken cancellationToken = default) =>
        UpsertAsync(Rows, record.Id, scope, Serialize(record), row => Index(row, record), cancellationToken);

    protected async Task SaveBatchAsync(TenantScope scope, IReadOnlyCollection<TRecord> records, CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            var row = await Rows.FindAsync([record.Id], cancellationToken);
            if (row is null)
            {
                row = new TRow { Id = record.Id };
                Rows.Add(row);
            }
            else if (row.TenantId != scope.TenantId || row.WorkspaceId != scope.WorkspaceId)
            {
                throw new TenantIsolationException();
            }

            ApplyScope(row, scope);
            row.PayloadJson = Serialize(record);
            Index(row, record);
        }

        // One save, so the batch appears together or not at all.
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfOrgWorkspaceStore(InsuranceIntelligenceDbContext db) : EfPlatformStoreBase<OrgWorkspace, PersistedOrgWorkspace>(db), IOrgWorkspaceStore
{
    protected override DbSet<PersistedOrgWorkspace> Rows => DbContext.OrgWorkspaces;

    protected override void Index(PersistedOrgWorkspace row, OrgWorkspace record) => row.IsPrimary = record.IsPrimary;

    // One check-then-insert at a time in this process. The unique index settles a race between instances on a relational
    // database; the in-memory provider has no such index, and a page that opens with several parallel requests would
    // otherwise create the primary workspace more than once.
    private static readonly SemaphoreSlim PrimaryGate = new(1, 1);

    public async Task<bool> TryCreatePrimaryAsync(TenantScope scope, OrgWorkspace workspace, CancellationToken cancellationToken = default)
    {
        await PrimaryGate.WaitAsync(cancellationToken);
        try
        {
            if (await Scoped(Rows.AsNoTracking(), scope).AnyAsync(r => r.IsPrimary, cancellationToken)) return false;
            var row = new PersistedOrgWorkspace { Id = workspace.Id, IsPrimary = true, PayloadJson = Serialize(workspace) };
            ApplyScope(row, scope);
            Rows.Add(row);
            try
            {
                await DbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException)
            {
                // The unique index on the primary workspace: another instance created it first.
                DbContext.Entry(row).State = EntityState.Detached;
                return false;
            }
        }
        finally
        {
            PrimaryGate.Release();
        }
    }
}

public sealed class EfPlatformAutomationStore(InsuranceIntelligenceDbContext db) : EfPlatformStoreBase<PlatformAutomation, PersistedPlatformAutomation>(db), IPlatformAutomationStore
{
    protected override DbSet<PersistedPlatformAutomation> Rows => DbContext.PlatformAutomations;

    protected override void Index(PersistedPlatformAutomation row, PlatformAutomation record)
    {
        row.OrgWorkspaceId = record.WorkspaceId;
        row.Status = record.Status;
    }

    public Task SaveManyAsync(TenantScope scope, IReadOnlyCollection<PlatformAutomation> records, CancellationToken cancellationToken = default) =>
        SaveBatchAsync(scope, records, cancellationToken);
}

public sealed class EfPlatformRunStore(InsuranceIntelligenceDbContext db) : EfPlatformStoreBase<PlatformAutomationRun, PersistedPlatformRun>(db), IPlatformRunStore
{
    protected override DbSet<PersistedPlatformRun> Rows => DbContext.PlatformAutomationRuns;

    protected override void Index(PersistedPlatformRun row, PlatformAutomationRun record)
    {
        row.AutomationId = record.AutomationId;
        row.StartedAt = record.StartedAt;
    }

    public async Task<IReadOnlyList<PlatformAutomationRun>> ListForAutomationAsync(TenantScope scope, Guid automationId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(Rows.AsNoTracking(), scope)
            .Where(r => r.AutomationId == automationId)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
        return rows.Select(row => Deserialize<PlatformAutomationRun>(row.PayloadJson)).ToList();
    }
}

public sealed class EfPlatformInboxStore(InsuranceIntelligenceDbContext db) : EfPlatformStoreBase<PlatformInboxItem, PersistedPlatformInboxItem>(db), IPlatformInboxStore
{
    protected override DbSet<PersistedPlatformInboxItem> Rows => DbContext.PlatformInboxItems;

    protected override void Index(PersistedPlatformInboxItem row, PlatformInboxItem record)
    {
        row.DedupeKey = record.DedupeKey;
        row.Status = record.Status;
    }

    public async Task<PlatformInboxItem?> FindByDedupeKeyAsync(TenantScope scope, string dedupeKey, CancellationToken cancellationToken = default)
    {
        var row = await Scoped(Rows.AsNoTracking(), scope)
            .Where(r => r.DedupeKey == dedupeKey)
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Deserialize<PlatformInboxItem>(row.PayloadJson);
    }
}

public sealed class EfPlatformIntentStore(InsuranceIntelligenceDbContext db) : EfPlatformStoreBase<PlatformIntent, PersistedPlatformIntent>(db), IPlatformIntentStore
{
    protected override DbSet<PersistedPlatformIntent> Rows => DbContext.PlatformIntents;
}

public sealed class EfPlatformSettingsStore(InsuranceIntelligenceDbContext db) : EfPlatformStoreBase<PlatformSettings, PersistedPlatformSettings>(db), IPlatformSettingsStore
{
    protected override DbSet<PersistedPlatformSettings> Rows => DbContext.PlatformSettings;
}
