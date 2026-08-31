using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfCommandCenterConversationRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICommandCenterConversationRepository
{
    public async Task SaveAsync(CommandCenterConversation conversation, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.CommandCenterConversations.FindAsync([conversation.ConversationId], cancellationToken);
        if (record is null)
        {
            if (conversation.Version != 1)
            {
                throw new CommandCenterConversationConflictException();
            }

            record = new PersistedCommandCenterConversation { Id = conversation.ConversationId };
            DbContext.CommandCenterConversations.Add(record);
        }
        else if (record.TenantId != conversation.Scope.TenantId
            || record.WorkspaceId != conversation.Scope.WorkspaceId
            || record.OwnerUserId != conversation.OwnerUserId
            || record.Version != conversation.Version - 1)
        {
            throw new CommandCenterConversationConflictException();
        }

        ApplyScope(record, conversation.Scope);
        record.OwnerUserId = conversation.OwnerUserId;
        record.Status = conversation.Status.ToString();
        record.LastActivityAt = conversation.LastActivityAt;
        record.PayloadJson = Serialize(conversation);
        record.Version = conversation.Version;
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CommandCenterConversationConflictException();
        }
    }

    public async Task<CommandCenterConversation?> GetAsync(
        Guid conversationId,
        TenantScope scope,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.CommandCenterConversations.AsNoTracking(), scope)
            .FirstOrDefaultAsync(
                item => item.Id == conversationId && item.OwnerUserId == ownerUserId,
                cancellationToken);
        return record is null ? null : Deserialize<CommandCenterConversation>(record.PayloadJson);
    }

    public async Task<IReadOnlyList<CommandCenterConversation>> ListAsync(
        TenantScope scope,
        string ownerUserId,
        CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.CommandCenterConversations.AsNoTracking(), scope)
            .Where(item => item.OwnerUserId == ownerUserId)
            .OrderByDescending(item => item.LastActivityAt)
            .Select(item => Deserialize<CommandCenterConversation>(item.PayloadJson))
            .ToArrayAsync(cancellationToken);
}
