using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Persistence for platform-operator tenant onboarding records. Onboarding is a platform-level
/// concept (it creates new tenants), so all records are stored under a fixed platform scope rather
/// than an insurer tenant. One JSON record per onboarding session, keyed by a deterministic id.
/// </summary>
public sealed class EfTenantOnboardingRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ITenantOnboardingRepository
{
    private static readonly TenantScope PlatformScope = TenantScope.Create("iip-platform", "onboarding");

    public Task UpsertAsync(TenantOnboardingRecord record, CancellationToken cancellationToken = default)
    {
        var id = DeterministicId(record.OnboardingId);
        return UpsertAsync(DbContext.TenantOnboardings, id, PlatformScope, Serialize(record), cancellationToken: cancellationToken);
    }

    public async Task<TenantOnboardingRecord?> GetAsync(string onboardingId, CancellationToken cancellationToken = default)
    {
        var all = await AllAsync(cancellationToken);
        return all.FirstOrDefault(r => string.Equals(r.OnboardingId, onboardingId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TenantOnboardingRecord?> GetByStateAsync(string state, CancellationToken cancellationToken = default)
    {
        var all = await AllAsync(cancellationToken);
        return all.FirstOrDefault(r => string.Equals(r.State, state, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyList<TenantOnboardingRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        await AllAsync(cancellationToken);

    private async Task<IReadOnlyList<TenantOnboardingRecord>> AllAsync(CancellationToken cancellationToken)
    {
        var payloads = await Scoped(DbContext.TenantOnboardings.AsNoTracking(), PlatformScope)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);
        return payloads.Select(Deserialize<TenantOnboardingRecord>).ToArray();
    }

    private static Guid DeterministicId(string onboardingId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"onboarding|{onboardingId.ToLowerInvariant()}")));
}

/// <summary>
/// Bootstraps the consenting admin's membership into IIP's own runtime-authority store
/// (ADR-0005): an active membership + an Admin role assignment for the new tenant/workspace.
/// Idempotent, repeated consent callbacks do not duplicate membership.
/// </summary>
public sealed class EfTenantMembershipWriter(InsuranceIntelligenceDbContext dbContext) : ITenantMembershipWriter
{
    public async Task CreateAdminMembershipAsync(string tenantId, string workspaceId, string userId, CancellationToken cancellationToken = default)
    {
        var hasMembership = await dbContext.UserTenantMemberships
            .AnyAsync(m => m.TenantId == tenantId && m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken);
        if (!hasMembership)
        {
            dbContext.UserTenantMemberships.Add(new UserTenantMembershipEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkspaceId = workspaceId,
                UserId = userId,
                IsActive = true,
            });
        }

        var hasRole = await dbContext.RoleAssignments
            .AnyAsync(r => r.TenantId == tenantId && r.WorkspaceId == workspaceId && r.UserId == userId && r.RoleCode == "Admin", cancellationToken);
        if (!hasRole)
        {
            dbContext.RoleAssignments.Add(new RoleAssignmentEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkspaceId = workspaceId,
                UserId = userId,
                RoleCode = "Admin",
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
