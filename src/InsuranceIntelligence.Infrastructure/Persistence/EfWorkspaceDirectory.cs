using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>Every workspace that has at least one active member. A workspace nobody can sign in to
/// has no work for a scheduled job to do.</summary>
public sealed class EfWorkspaceDirectory(InsuranceIntelligenceDbContext dbContext) : IWorkspaceDirectory
{
    public async Task<IReadOnlyList<TenantScope>> ListActiveWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var pairs = await dbContext.UserTenantMemberships.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new { m.TenantId, m.WorkspaceId })
            .Distinct()
            .ToListAsync(cancellationToken);
        return pairs.Select(pair => TenantScope.Create(pair.TenantId, pair.WorkspaceId)).ToArray();
    }
}
