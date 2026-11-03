using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfCustomerFollowUpRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICustomerFollowUpRepository
{
    public Task SaveAsync(CustomerFollowUpTask task, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.CustomerFollowUps, task.TaskId, task.Scope, Serialize(task), row =>
        {
            row.CustomerId = task.CustomerId;
            row.Channel = task.Channel;
            row.AccountId = task.AccountId;
            row.RecipientId = task.RecipientId;
            row.ThreadId = task.ThreadId;
            row.OriginatingMessageId = task.OriginatingMessageId;
            row.Status = task.Status.ToString();
            row.ClaimVersion++;
            row.DueAtUtc = task.DueAtUtc;
            row.ProcessedAt = task.ProcessedAt;
            row.CancelledAt = task.CancelledAt;
            row.CancellationReason = task.CancellationReason;
            row.ProviderMessageId = task.ProviderMessageId;
            row.AttemptCount = task.AttemptCount;
            row.SafeFailureReason = task.SafeFailureReason;
        }, cancellationToken);

    public async Task<CustomerFollowUpTask?> GetAsync(TenantScope scope, Guid taskId, CancellationToken cancellationToken = default)
    {
        var row = await Scoped(DbContext.CustomerFollowUps.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        return row is null ? null : Hydrate(row);
    }

    public async Task<IReadOnlyList<CustomerFollowUpTask>> ListAsync(
        TenantScope scope, string? customerId = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var query = Scoped(DbContext.CustomerFollowUps.AsNoTracking(), scope);
        if (!string.IsNullOrWhiteSpace(customerId)) query = query.Where(item => item.CustomerId == customerId.Trim());
        var rows = await query.OrderBy(item => item.DueAtUtc).Take(limit).ToArrayAsync(cancellationToken);
        return rows.Select(Hydrate).ToArray();
    }

    public async Task<IReadOnlyList<CustomerFollowUpTask>> TryClaimDueAsync(
        TenantScope scope, DateTimeOffset now, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var candidates = await Scoped(DbContext.CustomerFollowUps.AsNoTracking(), scope)
            .Where(item => item.Status == nameof(CustomerFollowUpStatus.Scheduled) && item.DueAtUtc <= now)
            .OrderBy(item => item.DueAtUtc)
            .Take(limit)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        var claimed = new List<CustomerFollowUpTask>(candidates.Length);
        foreach (var id in candidates)
        {
            if (DbContext.Database.IsRelational())
            {
                var changed = await Scoped(DbContext.CustomerFollowUps, scope)
                    .Where(item => item.Id == id && item.Status == nameof(CustomerFollowUpStatus.Scheduled)
                        && item.DueAtUtc <= now)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.Status, nameof(CustomerFollowUpStatus.Processing))
                        .SetProperty(item => item.UpdatedAt, now)
                        .SetProperty(item => item.ClaimVersion, item => item.ClaimVersion + 1), cancellationToken);
                if (changed != 1) continue;
                var row = await Scoped(DbContext.CustomerFollowUps.AsNoTracking(), scope)
                    .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (row is not null) claimed.Add(Hydrate(row));
                continue;
            }

            var localRow = await Scoped(DbContext.CustomerFollowUps, scope)
                .FirstOrDefaultAsync(item => item.Id == id
                    && item.Status == nameof(CustomerFollowUpStatus.Scheduled) && item.DueAtUtc <= now, cancellationToken);
            if (localRow is null) continue;
            localRow.Status = nameof(CustomerFollowUpStatus.Processing);
            localRow.UpdatedAt = now;
            localRow.ClaimVersion++;
            try
            {
                await DbContext.SaveChangesAsync(cancellationToken);
                claimed.Add(Hydrate(localRow));
                DbContext.Entry(localRow).State = EntityState.Detached;
            }
            catch (DbUpdateConcurrencyException)
            {
                DbContext.Entry(localRow).State = EntityState.Detached;
            }
        }

        return claimed;
    }

    public async Task<CustomerFollowUpTask?> TryCancelAsync(
        TenantScope scope, Guid taskId, DateTimeOffset now, string reason, CancellationToken cancellationToken = default)
    {
        var row = await Scoped(DbContext.CustomerFollowUps.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (row is null || row.Status is not (nameof(CustomerFollowUpStatus.PendingReview) or nameof(CustomerFollowUpStatus.Scheduled)))
        {
            return null;
        }

        var current = Hydrate(row);
        var cancelled = current with
        {
            Status = CustomerFollowUpStatus.Cancelled,
            UpdatedAt = now,
            CancelledAt = now,
            CancellationReason = reason,
        };

        if (DbContext.Database.IsRelational())
        {
            var changed = await Scoped(DbContext.CustomerFollowUps, scope)
                .Where(item => item.Id == taskId && item.ClaimVersion == row.ClaimVersion
                    && (item.Status == nameof(CustomerFollowUpStatus.PendingReview)
                        || item.Status == nameof(CustomerFollowUpStatus.Scheduled)))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, nameof(CustomerFollowUpStatus.Cancelled))
                    .SetProperty(item => item.UpdatedAt, now)
                    .SetProperty(item => item.CancelledAt, now)
                    .SetProperty(item => item.CancellationReason, reason)
                    .SetProperty(item => item.PayloadJson, Serialize(cancelled))
                    .SetProperty(item => item.ClaimVersion, item => item.ClaimVersion + 1), cancellationToken);
            return changed == 1 ? cancelled : null;
        }

        var tracked = await Scoped(DbContext.CustomerFollowUps, scope)
            .FirstOrDefaultAsync(item => item.Id == taskId && item.ClaimVersion == row.ClaimVersion
                && (item.Status == nameof(CustomerFollowUpStatus.PendingReview)
                    || item.Status == nameof(CustomerFollowUpStatus.Scheduled)), cancellationToken);
        if (tracked is null) return null;

        tracked.Status = nameof(CustomerFollowUpStatus.Cancelled);
        tracked.UpdatedAt = now;
        tracked.CancelledAt = now;
        tracked.CancellationReason = reason;
        tracked.PayloadJson = Serialize(cancelled);
        tracked.ClaimVersion++;
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            DbContext.Entry(tracked).State = EntityState.Detached;
            return cancelled;
        }
        catch (DbUpdateConcurrencyException)
        {
            DbContext.Entry(tracked).State = EntityState.Detached;
            return null;
        }
    }

    public async Task<int> CancelPendingForRecipientAsync(
        TenantScope scope, string channel, string accountId, string recipientId, DateTimeOffset now,
        string reason, CancellationToken cancellationToken = default)
    {
        var query = Scoped(DbContext.CustomerFollowUps, scope)
            .Where(item => item.Channel == channel && item.AccountId == accountId && item.RecipientId == recipientId
                && (item.Status == nameof(CustomerFollowUpStatus.Scheduled) || item.Status == nameof(CustomerFollowUpStatus.PendingReview)));
        if (DbContext.Database.IsRelational())
        {
            return await query.ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, nameof(CustomerFollowUpStatus.Cancelled))
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.CancelledAt, now)
                .SetProperty(item => item.CancellationReason, reason)
                .SetProperty(item => item.ClaimVersion, item => item.ClaimVersion + 1), cancellationToken);
        }

        var rows = await query.ToArrayAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Status = nameof(CustomerFollowUpStatus.Cancelled);
            row.UpdatedAt = now;
            row.CancelledAt = now;
            row.CancellationReason = reason;
            row.ClaimVersion++;
        }
        if (rows.Length > 0) await DbContext.SaveChangesAsync(cancellationToken);
        return rows.Length;
    }

    private CustomerFollowUpTask Hydrate(PersistedCustomerFollowUp row) => Deserialize<CustomerFollowUpTask>(row.PayloadJson) with
    {
        Status = Enum.Parse<CustomerFollowUpStatus>(row.Status, ignoreCase: false),
        DueAtUtc = row.DueAtUtc,
        UpdatedAt = row.UpdatedAt,
        ProcessedAt = row.ProcessedAt,
        CancelledAt = row.CancelledAt,
        CancellationReason = row.CancellationReason,
        ProviderMessageId = row.ProviderMessageId,
        AttemptCount = row.AttemptCount,
        SafeFailureReason = row.SafeFailureReason,
    };
}
