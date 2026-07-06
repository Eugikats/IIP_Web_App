using InsuranceIntelligence.Application.Tenants;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Persistence for the per-tenant workspace catalog (JSON payload). Workspaces are a
/// tenant-level concept, so this queries by tenant id only (not the tenant+workspace scope
/// filter used elsewhere). One record per workspace.
/// </summary>
public sealed class EfWorkspaceCatalogRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IWorkspaceCatalogRepository
{
    public async Task<IReadOnlyList<WorkspaceSummaryDto>> ListForTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var payloads = await DbContext.Workspaces.AsNoTracking()
            .Where(record => record.TenantId == tenantId)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        return payloads
            .Select(Deserialize<WorkspaceSummaryDto>)
            .OrderBy(workspace => workspace.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
