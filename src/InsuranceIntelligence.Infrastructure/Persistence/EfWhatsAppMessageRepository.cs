using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfWhatsAppMessageRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IWhatsAppMessageRepository
{
    public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.WhatsAppMessages, message.MessageId, message.Scope, Serialize(message), record =>
        {
            record.CaseId = message.CaseId;
            record.ThreadId = message.ThreadId;
            record.ProviderMessageId = message.ProviderMessageId;
            record.IdempotencyKey = message.IdempotencyKey;
            record.CreatedAt = message.CreatedAt;
        }, cancellationToken);

    public async Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.WhatsAppMessages.AsNoTracking(), scope)
            .SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        return record is null ? null : Deserialize<WhatsAppMessage>(record.PayloadJson);
    }

    public async Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.WhatsAppMessages.AsNoTracking(), scope)
            .SingleOrDefaultAsync(item => item.ProviderMessageId == providerMessageId, cancellationToken);
        return record is null ? null : Deserialize<WhatsAppMessage>(record.PayloadJson);
    }

    public async Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.WhatsAppMessages.AsNoTracking(), scope)
            .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        return record is null ? null : Deserialize<WhatsAppMessage>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<WhatsAppMessage>> ListSinceAsync(TenantScope scope, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var payloads = await Scoped(DbContext.WhatsAppMessages.AsNoTracking(), scope)
            .Where(item => item.CreatedAt >= since)
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);
        return payloads.Select(Deserialize<WhatsAppMessage>).ToArray();
    }

    public async Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default)
    {
        var payloads = await Scoped(DbContext.WhatsAppMessages.AsNoTracking(), scope)
            .Where(item => item.ThreadId == threadId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);
        return payloads.Select(Deserialize<WhatsAppMessage>).ToArray();
    }
}
