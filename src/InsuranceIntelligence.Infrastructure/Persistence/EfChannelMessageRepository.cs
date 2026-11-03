using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfChannelMessageRepository : JsonRepositoryBase, IChannelMessageRepository
{
    public EfChannelMessageRepository(InsuranceIntelligenceDbContext dbContext) : base(dbContext) { }

    public async Task<ChannelMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string channel, string accountId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var row = await DbContext.ChannelMessages.AsNoTracking().FirstOrDefaultAsync(x =>
            x.TenantId == scope.TenantId && x.WorkspaceId == scope.WorkspaceId && x.Channel == channel && x.AccountId == accountId
            && x.IdempotencyKey == idempotencyKey, cancellationToken);
        return row is null ? null : Deserialize<ChannelMessage>(row.PayloadJson);
    }
    public async Task<bool> SaveInboundIfNewAsync(ChannelMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Direction != ChannelMessageDirection.Inbound || string.IsNullOrWhiteSpace(message.ProviderMessageId))
            throw new ArgumentException("Inbound messages require a provider message ID.", nameof(message));

        var existing = await FindProviderMessageAsync(message.Scope, message.Channel, message.AccountId, message.ProviderMessageId, cancellationToken);
        if (existing is not null) return false;

        try
        {
            await SaveAsync(message, cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique provider-message index closes the race between duplicate webhook workers.
            var duplicate = await FindProviderMessageAsync(message.Scope, message.Channel, message.AccountId, message.ProviderMessageId, cancellationToken);
            if (duplicate is not null) return false;
            throw;
        }
    }

    public Task SaveAsync(ChannelMessage message, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.ChannelMessages, message.MessageId, message.Scope, Serialize(message), record =>
        {
            record.Channel = message.Channel;
            record.AccountId = message.AccountId;
            record.ThreadId = message.ThreadId;
            record.ExternalRecipientId = message.ExternalRecipientId;
            record.ProviderMessageId = message.ProviderMessageId;
            record.IdempotencyKey = message.IdempotencyKey;
            record.CreatedAt = message.CreatedAt;
        }, cancellationToken);

    public async Task<IReadOnlyList<ChannelMessage>> ListThreadAsync(
        TenantScope scope,
        string channel,
        string threadId,
        CancellationToken cancellationToken = default)
    {
        var payloads = await Scoped(DbContext.ChannelMessages.AsNoTracking(), scope)
            .Where(item => item.Channel == channel && item.ThreadId == threadId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);
        return payloads.Select(Deserialize<ChannelMessage>).ToArray();
    }

    public async Task<IReadOnlyList<ChannelMessage>> ListForExternalRecipientAsync(
        TenantScope scope, string channel, string accountId, string externalRecipientId,
        CancellationToken cancellationToken = default)
    {
        var payloads = await Scoped(DbContext.ChannelMessages.AsNoTracking(), scope)
            .Where(item => item.Channel == channel && item.AccountId == accountId && item.ExternalRecipientId == externalRecipientId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .Select(item => item.PayloadJson)
            .ToArrayAsync(cancellationToken);
        return payloads.Select(Deserialize<ChannelMessage>).ToArray();
    }

    private async Task<ChannelMessage?> FindProviderMessageAsync(
        TenantScope scope,
        string channel,
        string accountId,
        string providerMessageId,
        CancellationToken cancellationToken)
    {
        var record = await Scoped(DbContext.ChannelMessages.AsNoTracking(), scope)
            .SingleOrDefaultAsync(item => item.Channel == channel
                && item.AccountId == accountId
                && item.ProviderMessageId == providerMessageId, cancellationToken);
        return record is null ? null : Deserialize<ChannelMessage>(record.PayloadJson);
    }
}
