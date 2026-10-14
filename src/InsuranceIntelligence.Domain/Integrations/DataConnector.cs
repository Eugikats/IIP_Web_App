using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Domain.Integrations;

public enum ConnectorStatus
{
    Active,
    Paused,

    /// <summary>The owner's sign-in expired or was revoked. Nothing already synced is touched; the owner reconnects.</summary>
    NeedsReconnect,

    /// <summary>The last run failed for a reason other than the sign-in.</summary>
    Error,
}

public enum ConnectorRunOutcome
{
    Succeeded,
    Partial,
    Failed,
    SignInExpired,
}

public enum ConnectorItemState
{
    /// <summary>Brought in as a pending Knowledge document. A person still has to classify and approve it.</summary>
    Pending,

    /// <summary>Left out on purpose (unsupported type, too large, empty). The reason is kept.</summary>
    Skipped,

    /// <summary>No longer in the folder, or the folder was removed. It has been taken out of retrieval.</summary>
    Removed,

    /// <summary>Could not be read this run. It is tried again next run.</summary>
    Failed,
}

/// <summary>
/// A folder an administrator chose to sync, and what files from it are filed as. Never an approval. For Google Drive,
/// where the narrow permission IIP asks for cannot list a folder, the entry is one picked file (<c>IsFile</c>) and
/// <c>FolderId</c> holds that file's id.
/// </summary>
public sealed record ConnectorFolder(
    string FolderId,
    string DisplayName,
    string? DriveId,
    string ProductLine,
    KnowledgeDocumentType DocumentType,
    bool IsFile = false);

/// <summary>
/// A standing link from a folder in someone's Google Drive, OneDrive or SharePoint to the Data Hub. Reads use the
/// owner's own sign-in, so the connector can never see more than its owner can open. Files arrive as pending Knowledge
/// documents with unknown sensitivity; nothing a connector brings in is approved, indexed for customers or used to
/// ground an answer until a person decides.
/// </summary>
public sealed record DataConnector
{
    public required Guid ConnectorId { get; init; }
    public required TenantScope Scope { get; init; }
    public required FileProvider Provider { get; init; }
    public required string OwnerUserId { get; init; }
    public string? AccountEmail { get; init; }
    public IReadOnlyList<ConnectorFolder> Folders { get; init; } = [];
    public ConnectorStatus Status { get; init; } = ConnectorStatus.Active;

    /// <summary>Where the provider's change feed left off, so the next run asks only what changed.</summary>
    public string? ChangeCursor { get; init; }

    public required string CreatedBy { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public ConnectorRunOutcome? LastRunOutcome { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record ConnectorRun
{
    public required Guid RunId { get; init; }
    public required Guid ConnectorId { get; init; }
    public required TenantScope Scope { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public ConnectorRunOutcome? Outcome { get; init; }
    public int Found { get; init; }
    public int Added { get; init; }
    public int Updated { get; init; }
    public int Removed { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public string? ErrorCode { get; init; }

    /// <summary>A sentence safe to show an administrator. Never a raw provider error, token or file content.</summary>
    public string? SafeError { get; init; }
}

/// <summary>One file a connector has seen, and what was done with it. Names and ids only, never contents.</summary>
public sealed record ConnectorItem
{
    public required Guid ItemId { get; init; }
    public required Guid ConnectorId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string ExternalId { get; init; }
    public required string FolderId { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>The provider's version marker (ETag or head revision), to tell a changed file from an unchanged one.</summary>
    public string? Version { get; init; }

    public Guid? KnowledgeDocumentId { get; init; }
    public ConnectorItemState State { get; init; } = ConnectorItemState.Pending;
    public string? SkipReason { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }

    public static Guid StorageKey(Guid connectorId, string externalId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"connector-item:{connectorId:N}:{externalId}")));
}

public interface IDataConnectorRepository
{
    Task<DataConnector?> GetAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataConnector>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>Every active connector in every workspace, for the scheduled sync. Each carries its own scope.</summary>
    Task<IReadOnlyList<DataConnector>> ListActiveAcrossWorkspacesAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DataConnector connector, CancellationToken cancellationToken = default);

    Task DeleteAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default);
}

public interface IConnectorRunRepository
{
    Task SaveAsync(ConnectorRun run, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectorRun>> ListAsync(TenantScope scope, Guid connectorId, int limit, CancellationToken cancellationToken = default);
}

public interface IConnectorItemRepository
{
    Task<ConnectorItem?> GetAsync(TenantScope scope, Guid connectorId, string externalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectorItem>> ListAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default);

    Task SaveAsync(ConnectorItem item, CancellationToken cancellationToken = default);

    Task DeleteForConnectorAsync(TenantScope scope, Guid connectorId, CancellationToken cancellationToken = default);
}
