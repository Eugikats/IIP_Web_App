using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>The recent calls to the WhatsApp webhook, kept for 30 days so an administrator can see whether Meta is reaching IIP.</summary>
public sealed class EfWhatsAppWebhookReceiptRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IWhatsAppWebhookReceiptRepository
{
    private static readonly TimeSpan Keep = TimeSpan.FromDays(30);

    public async Task AddAsync(WhatsAppWebhookReceipt receipt, CancellationToken cancellationToken = default)
    {
        var record = new PersistedWhatsAppWebhookReceipt { ReceivedAt = receipt.ReceivedAt };
        ApplyScope(record, receipt.Scope);
        record.Id = receipt.Id;
        record.PayloadJson = Serialize(receipt);
        record.UpdatedAt = receipt.ReceivedAt;
        DbContext.WhatsAppWebhookReceipts.Add(record);

        // Old receipts leave a little at a time, with each new one.
        var cutoff = receipt.ReceivedAt - Keep;
        var old = await Scoped(DbContext.WhatsAppWebhookReceipts, receipt.Scope)
            .Where(item => item.ReceivedAt < cutoff)
            .OrderBy(item => item.ReceivedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        DbContext.WhatsAppWebhookReceipts.RemoveRange(old);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WhatsAppWebhookReceipt>> ListAsync(TenantScope scope, int limit, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.WhatsAppWebhookReceipts.AsNoTracking(), scope)
            .OrderByDescending(item => item.ReceivedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<WhatsAppWebhookReceipt>(record.PayloadJson)).ToArray();
    }
}
