using System.Collections.Concurrent;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryCommandCenterConversationRepository : ICommandCenterConversationRepository
{
    private readonly ConcurrentDictionary<Guid, CommandCenterConversation> conversations = new();

    public Task SaveAsync(CommandCenterConversation conversation, CancellationToken cancellationToken = default)
    {
        conversations[conversation.ConversationId] = conversation;
        return Task.CompletedTask;
    }

    public Task<CommandCenterConversation?> GetAsync(Guid conversationId, TenantScope scope, string ownerUserId, CancellationToken cancellationToken = default)
    {
        conversations.TryGetValue(conversationId, out var conversation);
        if (conversation is null || !conversation.Scope.Matches(scope) || !string.Equals(conversation.OwnerUserId, ownerUserId, StringComparison.Ordinal))
        {
            conversation = null;
        }

        return Task.FromResult(conversation);
    }

    public Task<IReadOnlyList<CommandCenterConversation>> ListAsync(TenantScope scope, string ownerUserId, CancellationToken cancellationToken = default)
    {
        var matches = conversations.Values
            .Where(item => item.Scope.Matches(scope) && string.Equals(item.OwnerUserId, ownerUserId, StringComparison.Ordinal))
            .OrderByDescending(item => item.LastActivityAt)
            .ToArray();
        return Task.FromResult<IReadOnlyList<CommandCenterConversation>>(matches);
    }
}
