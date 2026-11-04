using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// The durable job queue (build-loop item 17), kept in the database next to the data it works on.
/// Scheduling reads a few indexed columns (queue, status, when due); the full job is the JSON
/// payload. A worker takes a job by changing its lease token under an optimistic-concurrency check,
/// so on SQL Server two instances can never both take the same job. Everything a person sees is
/// scoped to their workspace; only the worker's lease crosses workspaces, and it only hands the job
/// back to the code that runs it in that job's own workspace.
/// </summary>
public sealed class EfBackgroundJobStore(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IBackgroundJobStore
{
    public async Task<BackgroundJob> EnqueueAsync(BackgroundJob job, bool coalesceWithRunning = true, CancellationToken cancellationToken = default)
    {
        var status = nameof(BackgroundJobStatus.Pending);
        var running = nameof(BackgroundJobStatus.Running);
        var existing = await Scoped(DbContext.BackgroundJobs.AsNoTracking(), job.Scope)
            .Where(record => record.Queue == job.Queue && record.Type == job.Type && record.Key == job.Key && (record.Status == status || (coalesceWithRunning && record.Status == running)))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return Deserialize<BackgroundJob>(existing.PayloadJson);
        }

        var stored = job with { LeaseToken = Guid.NewGuid() };
        var record = new PersistedBackgroundJob { Id = stored.JobId };
        ApplyScope(record, stored.Scope);
        Apply(record, stored);
        DbContext.BackgroundJobs.Add(record);
        await DbContext.SaveChangesAsync(cancellationToken);
        return stored;
    }

    public async Task<BackgroundJob?> LeaseNextAsync(IReadOnlyCollection<string> queues, string owner, TimeSpan lease, DateTimeOffset now, TenantScope? onlyScope = null, CancellationToken cancellationToken = default)
    {
        var pending = nameof(BackgroundJobStatus.Pending);
        var running = nameof(BackgroundJobStatus.Running);
        var candidates = onlyScope is { } only
            ? Scoped(DbContext.BackgroundJobs, only)
            : DbContext.BackgroundJobs;
        var record = await candidates
            .Where(item => queues.Contains(item.Queue)
                && ((item.Status == pending && item.NotBefore <= now) || (item.Status == running && item.LeasedUntil != null && item.LeasedUntil < now)))
            .OrderBy(item => item.NotBefore)
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            return null;
        }

        var job = Deserialize<BackgroundJob>(record.PayloadJson);
        var taken = job with
        {
            Status = BackgroundJobStatus.Running,
            Attempts = job.Attempts + 1,
            LeaseOwner = owner,
            LeasedUntil = now + lease,
            UpdatedAt = now,
            LeaseToken = Guid.NewGuid(),
        };
        Apply(record, taken);
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return taken;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker took it between the read and the write. That is fine: it has it.
            DbContext.Entry(record).State = EntityState.Detached;
            return null;
        }
    }

    public async Task<bool> SaveHeldAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.BackgroundJobs.FirstOrDefaultAsync(item => item.Id == job.JobId, cancellationToken);
        if (record is null || record.LeaseToken != job.LeaseToken)
        {
            return false;
        }

        // A new token on every save, so a worker that comes back late cannot overwrite a newer outcome.
        Apply(record, job with { LeaseToken = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow });
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            DbContext.Entry(record).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<BackgroundJob?> GetAsync(TenantScope scope, Guid jobId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.BackgroundJobs.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        return record is null ? null : Deserialize<BackgroundJob>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<BackgroundJob>> ListAsync(TenantScope scope, BackgroundJobStatus? status, Guid? caseId, int take, CancellationToken cancellationToken = default)
    {
        var query = Scoped(DbContext.BackgroundJobs.AsNoTracking(), scope);
        if (status is { } wanted)
        {
            var name = wanted.ToString();
            query = query.Where(item => item.Status == name);
        }

        if (caseId is { } id)
        {
            query = query.Where(item => item.CaseId == id);
        }

        var records = await query.OrderByDescending(item => item.NotBefore).Take(Math.Clamp(take, 1, 500)).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<BackgroundJob>(record.PayloadJson)).OrderByDescending(job => job.CreatedAt).ToArray();
    }

    public async Task<IReadOnlyDictionary<BackgroundJobStatus, int>> CountAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var counts = await Scoped(DbContext.BackgroundJobs.AsNoTracking(), scope)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return counts.ToDictionary(item => Enum.Parse<BackgroundJobStatus>(item.Status), item => item.Count);
    }

    public async Task<BackgroundJob?> RequeueAsync(TenantScope scope, Guid jobId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.BackgroundJobs, scope).FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var job = Deserialize<BackgroundJob>(record.PayloadJson);
        if (job.Status != BackgroundJobStatus.DeadLettered)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var fresh = job with
        {
            Status = BackgroundJobStatus.Pending,
            Attempts = 0,
            NotBefore = now,
            LeaseOwner = null,
            LeasedUntil = null,
            CompletedAt = null,
            UpdatedAt = now,
            LeaseToken = Guid.NewGuid(),
        };
        Apply(record, fresh);
        await DbContext.SaveChangesAsync(cancellationToken);
        return fresh;
    }

    private static void Apply(PersistedBackgroundJob record, BackgroundJob job)
    {
        record.PayloadJson = Serialize(job);
        record.Queue = job.Queue;
        record.Type = job.Type;
        record.Key = job.Key;
        record.Status = job.Status.ToString();
        record.NotBefore = job.NotBefore;
        record.LeasedUntil = job.LeasedUntil;
        record.CaseId = job.CaseId;
        record.LeaseToken = job.LeaseToken;
        record.UpdatedAt = job.UpdatedAt;
    }
}
