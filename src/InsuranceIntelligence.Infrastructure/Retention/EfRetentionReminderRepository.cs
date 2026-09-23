using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Retention;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Retention;

public sealed class EfRetentionReminderRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IRetentionReminderRepository
{
    public Task SaveAsync(RetentionReminderDraft reminder, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.RetentionReminders,
            reminder.ReminderId,
            reminder.Scope,
            Serialize(reminder),
            persisted => persisted.PolicyId = reminder.PolicyId,
            cancellationToken);

    public async Task<RetentionReminderDraft?> GetAsync(Guid reminderId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.RetentionReminders.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == reminderId, cancellationToken);
        return record is null ? null : Deserialize<RetentionReminderDraft>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<RetentionReminderDraft>> ListForPolicyAsync(string policyId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.RetentionReminders.AsNoTracking(), scope)
            .Where(item => item.PolicyId == policyId)
            .ToArrayAsync(cancellationToken);
        return records.Select(item => Deserialize<RetentionReminderDraft>(item.PayloadJson)).ToArray();
    }

    public async Task<IReadOnlyCollection<RetentionReminderDraft>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.RetentionReminders.AsNoTracking(), scope)
            .Select(item => Deserialize<RetentionReminderDraft>(item.PayloadJson))
            .ToArrayAsync(cancellationToken);
}
