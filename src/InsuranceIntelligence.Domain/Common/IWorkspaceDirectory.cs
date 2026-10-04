namespace InsuranceIntelligence.Domain.Common;

/// <summary>
/// The workspaces that are live on this deployment, for the work that has to visit each insurer in
/// turn (scheduled jobs). Platform-level on purpose and never exposed through an API: asking "which
/// tenants exist" is the one cross-tenant question the platform itself has to be able to answer.
/// </summary>
public interface IWorkspaceDirectory
{
    Task<IReadOnlyList<TenantScope>> ListActiveWorkspacesAsync(CancellationToken cancellationToken = default);
}
