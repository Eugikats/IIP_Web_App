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

    /// <summary>Replace a record only if its current payload still matches, for bounded security counters.</summary>
    Task<bool> TryReplaceAsync(TenantScope scope, string kind, string key, string expectedPayloadJson,
        string replacementPayloadJson, CancellationToken cancellationToken = default) => Task.FromResult(false);

    Task<IReadOnlyList<WorkspaceRecord>> ListAsync(TenantScope scope, string kind, int take = 500, CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantScope scope, string kind, string key, CancellationToken cancellationToken = default);

    Task<int> CountAsync(TenantScope scope, string kind, CancellationToken cancellationToken = default);

    /// <summary>Lists records across workspaces for a named platform operation. Callers must
    /// enforce platform-level authorization before using this method. Use only for queues whose
    /// owning workflow explicitly spans tenants, and return the minimum fields needed.</summary>
    Task<IReadOnlyList<(TenantScope Scope, WorkspaceRecord Record)>> ListPlatformAsync(string kind, int take = 500, CancellationToken cancellationToken = default);
}
