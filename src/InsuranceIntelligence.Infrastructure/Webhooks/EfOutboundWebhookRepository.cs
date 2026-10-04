using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Webhooks;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Webhooks;

public sealed class EfOutboundWebhookRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IOutboundWebhookRepository
{
    public Task SaveSubscriptionAsync(OutboundWebhookSubscription subscription, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.OutboundWebhookSubscriptions, subscription.SubscriptionId, subscription.Scope, Serialize(subscription), cancellationToken: cancellationToken);

    public async Task<OutboundWebhookSubscription?> GetSubscriptionAsync(TenantScope scope, Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.OutboundWebhookSubscriptions.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == subscriptionId, cancellationToken);
        return record is null ? null : Deserialize<OutboundWebhookSubscription>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<OutboundWebhookSubscription>> ListSubscriptionsAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var payloads = await Scoped(DbContext.OutboundWebhookSubscriptions.AsNoTracking(), scope)
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);
        return payloads.Select(Deserialize<OutboundWebhookSubscription>).ToArray();
    }

    public async Task DeleteSubscriptionAsync(TenantScope scope, Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.OutboundWebhookSubscriptions, scope).FirstOrDefaultAsync(item => item.Id == subscriptionId, cancellationToken);
        if (record is not null)
        {
            DbContext.OutboundWebhookSubscriptions.Remove(record);
            var deliveries = await Scoped(DbContext.OutboundDeliveries, scope).Where(item => item.SubscriptionId == subscriptionId).ToListAsync(cancellationToken);
            DbContext.OutboundDeliveries.RemoveRange(deliveries);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task SaveDeliveryAsync(OutboundDelivery delivery, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.OutboundDeliveries, delivery.DeliveryId, delivery.Scope, Serialize(delivery), record =>
        {
            record.SubscriptionId = delivery.SubscriptionId;
            record.Status = delivery.Status.ToString();
            record.NextAttemptAt = delivery.NextAttemptAt;
            record.CreatedAt = delivery.CreatedAt;
        }, cancellationToken);

    public async Task<IReadOnlyList<OutboundDelivery>> ListDeliveriesAsync(TenantScope scope, Guid subscriptionId, int take, CancellationToken cancellationToken = default)
    {
        var payloads = await Scoped(DbContext.OutboundDeliveries.AsNoTracking(), scope)
            .Where(item => item.SubscriptionId == subscriptionId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);
        return payloads.Select(Deserialize<OutboundDelivery>).ToArray();
    }

    public async Task<IReadOnlyList<OutboundDelivery>> ListDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default)
    {
        var pending = OutboundDeliveryStatus.Pending.ToString();
        var payloads = await DbContext.OutboundDeliveries.AsNoTracking()
            .Where(item => item.Status == pending && item.NextAttemptAt <= now)
            .OrderBy(item => item.NextAttemptAt)
            .Take(take)
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);
        return payloads.Select(Deserialize<OutboundDelivery>).ToArray();
    }
}
