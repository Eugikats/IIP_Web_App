using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Workspaces;

/// <summary>One small JSON record a workspace keeps about itself: its brand, its templates, its rules. The kind
/// says what it is; the key says which one.</summary>
public sealed record WorkspaceRecord(string Kind, string Key, string PayloadJson, DateTimeOffset UpdatedAt);

/// <summary>
/// A tenant- and workspace-scoped store of small JSON records. Every call takes the scope, so a record can only be read
/// or written from the workspace that owns it; there is no call that crosses workspaces. Reads treat a store that has
/// not been migrated yet as empty, so a release that ships ahead of its migration shows an empty workspace instead of
/// failing; writes do not, because a lost write must be seen.
/// </summary>
public interface IWorkspaceRecordStore
{
    Task<WorkspaceRecord?> GetAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default);

    Task SaveAsync(TenantScope scope, string kind, string key, string payloadJson, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceRecord>> ListAsync(TenantScope scope, string kind, int take = 500, CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default);

    Task<int> CountAsync(TenantScope scope, string kind, CancellationToken cancellationToken = default);
}
