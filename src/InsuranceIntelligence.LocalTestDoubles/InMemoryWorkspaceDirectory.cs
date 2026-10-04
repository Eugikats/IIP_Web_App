using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryWorkspaceDirectory(params TenantScope[] workspaces) : IWorkspaceDirectory
{
    public Task<IReadOnlyList<TenantScope>> ListActiveWorkspacesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TenantScope>>(workspaces);
}
