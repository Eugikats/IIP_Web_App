namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>A file as the provider reports it. Names, sizes and version markers only, never contents.</summary>
public sealed record RemoteFile(
    string ExternalId,
    string Name,
    string FolderId,
    string? MimeType,
    long Size,
    DateTimeOffset? ModifiedAt,
    string? Version,
    string? DriveId = null);

/// <summary>
/// What a provider said a folder (or a picked file) holds right now. <c>EmptyIsAuthoritative</c> is true only when the
/// provider positively said the thing is gone (a picked file that returns "not found" or is in the bin). An empty
/// listing from a folder is not authoritative: it is as likely a permissions or sign-in hiccup as an emptied folder,
/// and deleting everything that was synced on that evidence would be the worst way to be wrong.
/// </summary>
public sealed record ConnectorListing(IReadOnlyList<RemoteFile> Files, bool EmptyIsAuthoritative);

/// <summary>
/// A failure a sync can tell apart. <c>sign_in_expired</c>: the owner must reconnect. <c>folder_unavailable</c>: this
/// folder cannot be read now (moved, no access), so what was synced from it stays as it is. <c>provider_error</c>:
/// try again later. The message is safe to show; provider bodies and tokens never go in it.
/// </summary>
public sealed class ConnectorSourceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Reads one provider's folders and files with an access token the sync already has.</summary>
public interface IConnectorFileSource
{
    FileProvider Provider { get; }

    Task<ConnectorListing> ListAsync(string accessToken, ConnectorFolder folder, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadAsync(string accessToken, RemoteFile file, CancellationToken cancellationToken = default);
}
