namespace InsuranceIntelligence.Application.Tenants;

/// <summary>
/// Read-only catalog of the workspaces available within a tenant. This is the Gate 0
/// multi-workspace data model: the model + `/me` availability list exist now, so a workspace
/// switcher can be added later without a schema change. Access is scoped to the caller's tenant;
/// a per-workspace membership model is a later-gate refinement.
/// </summary>
public interface IWorkspaceCatalogService
{
    Task<IReadOnlyList<WorkspaceSummaryDto>> ListForTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}

public interface IWorkspaceCatalogRepository
{
    Task<IReadOnlyList<WorkspaceSummaryDto>> ListForTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceSummaryDto(
    string WorkspaceId,
    string DisplayName,
    string Region);
