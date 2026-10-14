using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Integrations;

/// <summary>Where a person's own file connection stands. "Configured" is about the platform (an app
/// registration exists for the provider); "Connected" is about this person.</summary>
public sealed record FileConnectionStatus(string Provider, bool Configured, bool Connected, string? AccountEmail, string? Reason);

public sealed record FileConnectStart(string AuthorizeUrl);

public sealed record FileConnectResult(string Provider, string? AccountEmail, string? ReturnTo);

public sealed record FileItem(string Id, string Name, bool IsFolder, long? Size, string? MimeType, DateTimeOffset? ModifiedAt);

public sealed record FileBrowseResult(string? FolderId, IReadOnlyList<FileItem> Items);

public sealed record DownloadedFile(string Name, byte[] Content, string ContentType);

/// <summary>What the browser needs to open Google's own file picker: a short-lived token for the
/// person's account, and the project the picker shares the chosen files with.</summary>
public sealed record GooglePickerSession(string AccessToken, string AppId, string? DeveloperKey, string? AccountEmail);

public sealed class FileConnectionException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// A person's own Google Drive and Microsoft OneDrive, used to pick a customer list or claim document and
/// to save exports. Every call acts as the signed-in person through their own connection. Nothing here
/// reads another person's files, and the tokens never reach the browser.
/// </summary>
public interface IUserFileService : IFileAccessTokenProvider
{
    Task<IReadOnlyList<FileConnectionStatus>> StatusAsync(CancellationToken cancellationToken = default);

    Task<FileConnectStart> StartAsync(FileProvider provider, string? returnTo, bool includeSharePoint, CancellationToken cancellationToken = default);

    Task<FileConnectResult> ExchangeAsync(FileProvider provider, string code, string state, CancellationToken cancellationToken = default);

    Task DisconnectAsync(FileProvider provider, CancellationToken cancellationToken = default);

    Task<FileBrowseResult> BrowseAsync(FileProvider provider, string? folderId, CancellationToken cancellationToken = default);

    Task<DownloadedFile> DownloadAsync(FileProvider provider, string fileId, CancellationToken cancellationToken = default);

    Task<GooglePickerSession> PickerSessionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A short-lived access token for one named person's connection, for work that runs without a signed-in request
/// (a connector sync). It never changes what that person can open: the token is theirs. Failures keep their cause
/// apart, so a caller can tell a sign-in that has expired or been revoked (code <c>sign_in_expired</c>, the person must
/// reconnect, and nothing already synced is removed) from a missing connection (<c>not_connected</c>) and from a
/// temporary provider problem (<c>provider_error</c>, try again later).
/// </summary>
public interface IFileAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default);
}
