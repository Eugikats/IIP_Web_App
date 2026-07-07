namespace InsuranceIntelligence.Application.Tenants;

/// <summary>
/// Resolves the workspaces available within a tenant. If the catalog has no persisted
/// workspaces yet, callers fall back to the caller's single resolved workspace — the honest
/// one-workspace-per-instance default. No fabrication.
/// </summary>
public sealed class WorkspaceCatalogService(IWorkspaceCatalogRepository repository) : IWorkspaceCatalogService
{
    public async Task<IReadOnlyList<WorkspaceSummaryDto>> ListForTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return [];
        }

        return await repository.ListForTenantAsync(tenantId.Trim(), cancellationToken);
    }
}
