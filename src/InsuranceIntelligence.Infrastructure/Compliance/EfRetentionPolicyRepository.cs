using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Compliance;

/// <summary>Current-state, one-row-per-(tenant, data class) — same pattern as
/// EfWhatsAppChannelConfigurationRepository, not the append-only versioned pattern
/// EfAgentBehaviourProfileRepository uses: a retention *setting* changing is not itself the
/// compliance-relevant fact, the deletions RetentionEnforcementService performs are, and those
/// already get their own audit trail.</summary>
public sealed class EfRetentionPolicyRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IRetentionPolicyRepository
{
    public async Task<RetentionPolicy?> GetAsync(TenantScope scope, RetentionDataClass dataClass, CancellationToken cancellationToken = default)
    {
        var id = RetentionPolicy.StorageKey(scope, dataClass);
        var record = await Scoped(DbContext.RetentionPolicies.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<RetentionPolicy>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<RetentionPolicy>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.RetentionPolicies.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records.Select(record => Deserialize<RetentionPolicy>(record.PayloadJson)).ToArray();
    }

    public Task SaveAsync(RetentionPolicy policy, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.RetentionPolicies,
            RetentionPolicy.StorageKey(policy.Scope, policy.DataClass),
            policy.Scope,
            Serialize(policy),
            cancellationToken: cancellationToken);
}
