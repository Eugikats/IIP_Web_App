using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.CustomerAssistant;

/// <summary>Append-only, same pattern as EfAuditRepository/EfAgentBehaviourProfileRepository —
/// every SaveAsync inserts a new row keyed by a fresh TurnId, so the full conversation history for
/// a thread is always reconstructable.</summary>
public sealed class EfCustomerConversationRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICustomerConversationRepository
{
    public Task SaveAsync(CustomerConversationTurn turn, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.CustomerConversationTurns,
            turn.TurnId,
            turn.Scope,
            Serialize(turn),
            record => record.ThreadId = turn.ThreadId,
            cancellationToken);

    public async Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.CustomerConversationTurns.AsNoTracking(), scope)
            .Where(record => record.ThreadId == threadId)
            .ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<CustomerConversationTurn>(record.PayloadJson))
            .OrderBy(turn => turn.OccurredAt)
            .ToArray();
    }
}
