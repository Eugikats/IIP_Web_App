using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfChannelIdentityLinkRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IChannelIdentityLinkRepository
{
    public async Task<ChannelIdentityLink?> FindByExternalIdentityAsync(
        TenantScope scope, string channel, string accountId, string externalRecipientId,
        CancellationToken cancellationToken = default)
    {
        var row = await Scoped(DbContext.ChannelIdentityLinks.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Channel == channel
                && item.AccountId == accountId
                && item.ExternalRecipientId == externalRecipientId, cancellationToken);
        return row is null ? null : Deserialize<ChannelIdentityLink>(row.PayloadJson);
    }

    public async Task<ChannelIdentityLink?> GetAsync(TenantScope scope, Guid linkId, CancellationToken cancellationToken = default)
    {
        var row = await Scoped(DbContext.ChannelIdentityLinks.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == linkId, cancellationToken);
        return row is null ? null : Deserialize<ChannelIdentityLink>(row.PayloadJson);
    }

    public async Task<IReadOnlyList<ChannelIdentityLink>> ListForCustomerAsync(
        TenantScope scope, string customerId, CancellationToken cancellationToken = default)
    {
        var payloads = await Scoped(DbContext.ChannelIdentityLinks.AsNoTracking(), scope)
            .Where(item => item.CustomerId == customerId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Channel)
            .Select(item => item.PayloadJson)
            .ToArrayAsync(cancellationToken);
        return payloads.Select(Deserialize<ChannelIdentityLink>).ToArray();
    }

    public Task SaveAsync(ChannelIdentityLink link, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.ChannelIdentityLinks, link.LinkId, link.Scope, Serialize(link), row =>
        {
            row.CustomerId = link.CustomerId;
            row.Channel = link.Channel;
            row.AccountId = link.AccountId;
            row.ExternalRecipientId = link.ExternalRecipientId;
            row.IsActive = link.IsActive;
        }, cancellationToken);
}
