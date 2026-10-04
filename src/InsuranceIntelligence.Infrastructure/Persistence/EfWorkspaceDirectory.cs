using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>Every workspace that has at least one active member. A workspace nobody can sign in to
/// has no work for a scheduled job to do.</summary>
public sealed class EfWorkspaceDirectory(InsuranceIntelligenceDbContext dbContext) : IWorkspaceDirectory
{
    public async Task<IReadOnlyList<TenantScope>> ListActiveWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        // Sandbox workspaces hold made-up customers: reminders and retention sweeps must never run there.
        var pairs = await dbContext.UserTenantMemberships.AsNoTracking()
            .Where(m => m.IsActive && m.WorkspaceId != "sandbox")
            .Select(m => new { m.TenantId, m.WorkspaceId })
            .Distinct()
            .ToListAsync(cancellationToken);
        // Only organisations that are live: a suspended or cancelled subscription is not worked for.
        var inactive = (await dbContext.Organisations.AsNoTracking()
            .Where(o => o.Status != "Active")
            .Select(o => o.OrganisationId)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        return pairs.Where(pair => !inactive.Contains(pair.TenantId)).Select(pair => TenantScope.Create(pair.TenantId, pair.WorkspaceId)).ToArray();
    }
}
