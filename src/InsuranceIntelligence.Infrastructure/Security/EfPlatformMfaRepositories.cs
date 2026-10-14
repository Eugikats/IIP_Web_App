using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Security;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Security;

/// <summary>One document per person: their set-up methods, any code waiting to be typed back, and whether they are verified now.</summary>
public sealed class EfUserSecurityRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IUserSecurityRepository
{
    public async Task<UserSecurityProfile?> GetAsync(TenantScope scope, string userId, CancellationToken cancellationToken = default)
    {
        var id = UserSecurityProfile.StorageKey(scope, userId);
        var record = await Scoped(DbContext.UserSecurityProfiles.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<UserSecurityProfile>(record.PayloadJson);
    }

    public Task SaveAsync(UserSecurityProfile profile, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.UserSecurityProfiles,
            UserSecurityProfile.StorageKey(profile.Scope, profile.UserId),
            profile.Scope,
            Serialize(profile),
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<UserSecurityProfile>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.UserSecurityProfiles.AsNoTracking(), scope).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<UserSecurityProfile>(record.PayloadJson)).ToArray();
    }
}

public sealed class EfMfaPolicyRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IMfaPolicyRepository
{
    public async Task<MfaPolicy?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = MfaPolicy.StorageKey(scope);
        try
        {
            var record = await Scoped(DbContext.MfaPolicies.AsNoTracking(), scope)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return record is null ? null : Deserialize<MfaPolicy>(record.PayloadJson);
        }
        catch (Microsoft.Data.SqlClient.SqlException failure) when (failure.Number == 208)
        {
            // The table is created by the AddPlatformMfa migration. Until it is applied there is no policy, which means the
            // default (off), so a release that ships ahead of its migration cannot lock everyone out, including the
            // administrator who needs to apply it.
            return null;
        }
    }

    public Task SaveAsync(MfaPolicy policy, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.MfaPolicies, MfaPolicy.StorageKey(policy.Scope), policy.Scope, Serialize(policy), cancellationToken: cancellationToken);
}
