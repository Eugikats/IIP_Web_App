using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.CustomerAssistant;

public sealed class EfConversationActivityReader(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IConversationActivityReader
{
    public async Task<IReadOnlyList<CustomerConversationTurn>> ListTurnsAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.CustomerConversationTurns.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records.Select(record => Deserialize<CustomerConversationTurn>(record.PayloadJson)).OrderBy(turn => turn.OccurredAt).ToArray();
    }
}
