using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Agents;

/// <summary>
/// Append-only, exactly like EfAuditRepository: every SaveAsync call inserts a new row keyed by a
/// fresh VersionId, never overwrites a previous one — that is what makes real version history
/// possible without a separate history table.
/// </summary>
public sealed class EfAgentBehaviourProfileRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentBehaviourProfileRepository
{
    public async Task<AgentBehaviourProfile?> GetCurrentAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default)
    {
        var versions = await ListVersionsAsync(scope, agentKey, cancellationToken);
        return versions.FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<AgentBehaviourProfile>> ListVersionsAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.AgentBehaviourProfiles.AsNoTracking(), scope)
            .Where(record => record.AgentKey == agentKey)
            .ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<AgentBehaviourProfile>(record.PayloadJson))
            .OrderByDescending(profile => profile.Version)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<AgentBehaviourProfile>> ListCurrentForAllAgentsAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.AgentBehaviourProfiles.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<AgentBehaviourProfile>(record.PayloadJson))
            .GroupBy(profile => profile.AgentKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(profile => profile.Version).First())
            .ToArray();
    }

    public Task SaveAsync(AgentBehaviourProfile version, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AgentBehaviourProfiles,
            version.VersionId,
            version.Scope,
            Serialize(version),
            record => record.AgentKey = version.AgentKey,
            cancellationToken);
}
