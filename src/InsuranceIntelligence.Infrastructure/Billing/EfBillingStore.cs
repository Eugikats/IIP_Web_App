using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Billing;

/// <summary>
/// Plans, invoices and payment attempts. Every read and write is inside one workspace, except the three calls
/// the platform's own timers and the payment callback need, which are named for it and return whole records
/// that carry their own scope.
/// </summary>
public sealed class EfBillingStore(InsuranceIntelligenceDbContext dbContext) : JsonRepositoryBase(dbContext), ISubscriptionStore, IPaymentStore
{
    // One subscription per workspace: its row id is derived from the scope, so a second save replaces the first.
    private static Guid SubscriptionId(TenantScope scope)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"subscription|{scope.TenantId}|{scope.WorkspaceId}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public async Task<Subscription?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.Subscriptions.AsNoTracking(), scope).FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : Deserialize<Subscription>(record.PayloadJson);
    }

    public Task SaveAsync(Subscription subscription, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.Subscriptions,
            SubscriptionId(subscription.Scope),
            subscription.Scope,
            Serialize(subscription),
            record =>
            {
                record.PlanKey = subscription.PlanKey;
                record.Status = subscription.Status;
                record.PeriodEndUtc = subscription.PeriodEnd?.UtcDateTime;
            },
            cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListPaidEndingBeforeAsync(DateTimeOffset moment, CancellationToken cancellationToken = default)
    {
        var limit = moment.UtcDateTime;
        var rows = await DbContext.Subscriptions.AsNoTracking()
            .Where(record => record.PeriodEndUtc != null && record.PeriodEndUtc <= limit)
            .Select(record => record.PayloadJson)
            .ToListAsync(cancellationToken);
        return rows.Select(Deserialize<Subscription>).ToArray();
    }

    public Task SaveIntentAsync(PaymentIntent intent, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.PaymentIntents,
            intent.PaymentIntentId,
            intent.Scope,
            Serialize(intent),
            record =>
            {
                record.Status = intent.Status;
                record.CreatedAtUtc = intent.CreatedAt.UtcDateTime;
            },
            cancellationToken);

    public async Task<PaymentIntent?> GetIntentAsync(TenantScope scope, Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.PaymentIntents.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == paymentIntentId, cancellationToken);
        return record is null ? null : Deserialize<PaymentIntent>(record.PayloadJson);
    }

    public async Task<PaymentIntent?> FindIntentByIdAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.PaymentIntents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == paymentIntentId, cancellationToken);
        return record is null ? null : Deserialize<PaymentIntent>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<PaymentIntent>> ListIntentsAsync(TenantScope scope, int take, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(DbContext.PaymentIntents.AsNoTracking(), scope)
            .OrderByDescending(record => record.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(record => record.PayloadJson)
            .ToListAsync(cancellationToken);
        return rows.Select(Deserialize<PaymentIntent>).ToArray();
    }

    public async Task<IReadOnlyList<PaymentIntent>> ListPendingBeforeAsync(DateTimeOffset moment, int take, CancellationToken cancellationToken = default)
    {
        var limit = moment.UtcDateTime;
        var rows = await DbContext.PaymentIntents.AsNoTracking()
            .Where(record => record.Status == PaymentStatuses.Pending && record.CreatedAtUtc <= limit)
            .OrderBy(record => record.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(record => record.PayloadJson)
            .ToListAsync(cancellationToken);
        return rows.Select(Deserialize<PaymentIntent>).ToArray();
    }

    public Task SaveInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.Invoices,
            invoice.InvoiceId,
            invoice.Scope,
            Serialize(invoice),
            record =>
            {
                record.Number = invoice.Number;
                record.Status = invoice.Status;
                record.IssuedAtUtc = invoice.IssuedAt.UtcDateTime;
            },
            cancellationToken);

    public async Task<Invoice?> GetInvoiceAsync(TenantScope scope, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.Invoices.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        return record is null ? null : Deserialize<Invoice>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<Invoice>> ListInvoicesAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(DbContext.Invoices.AsNoTracking(), scope)
            .OrderByDescending(record => record.IssuedAtUtc)
            .Select(record => record.PayloadJson)
            .ToListAsync(cancellationToken);
        return rows.Select(Deserialize<Invoice>).ToArray();
    }
}
