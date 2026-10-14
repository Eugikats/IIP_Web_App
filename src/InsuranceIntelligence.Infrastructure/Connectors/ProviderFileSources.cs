using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Connections;

namespace InsuranceIntelligence.Infrastructure.Connectors;

/// <summary>
/// Reads a folder in the owner's OneDrive or SharePoint through Microsoft Graph, as the owner, so it sees what that
/// person can open and no more. Folders are walked to a bounded depth and size. A provider body, token or file content
/// never goes into an exception message.
/// </summary>
public sealed class MicrosoftGraphFileSource(IHttpClientFactory httpClientFactory) : IConnectorFileSource
{
    public const int MaxDepth = 8;
    public const int MaxFiles = 5000;
    private const string Graph = "https://graph.microsoft.com/v1.0";

    public FileProvider Provider => FileProvider.Microsoft;

    public async Task<ConnectorListing> ListAsync(string accessToken, ConnectorFolder folder, CancellationToken cancellationToken = default)
    {
        if (folder.IsFile)
        {
            throw new ConnectorSourceException("folder_unavailable", "A single file cannot be synced from OneDrive. Choose its folder.");
        }

        var files = new List<RemoteFile>();
        var pending = new Queue<(string ItemId, int Depth)>();
        pending.Enqueue((folder.FolderId, 0));
        while (pending.Count > 0)
        {
            var (itemId, depth) = pending.Dequeue();
            var url = $"{DriveRoot(folder)}/items/{Uri.EscapeDataString(itemId)}/children?$select=id,name,size,folder,file,lastModifiedDateTime,eTag&$top=200";
            while (url is not null)
            {
                using var document = await GetJsonAsync(accessToken, url, isRoot: itemId == folder.FolderId, cancellationToken);
                foreach (var item in document.RootElement.GetProperty("value").EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString() ?? string.Empty;
                    if (item.TryGetProperty("folder", out _))
                    {
                        if (depth + 1 <= MaxDepth) pending.Enqueue((id, depth + 1));
                        continue;
                    }

                    if (files.Count >= MaxFiles)
                    {
                        throw new ConnectorSourceException("folder_unavailable", $"The folder holds more than {MaxFiles} files, which is more than a connector reads. Choose a smaller folder.");
                    }

                    files.Add(new RemoteFile(
                        id,
                        item.TryGetProperty("name", out var name) ? name.GetString() ?? id : id,
                        folder.FolderId,
                        item.TryGetProperty("file", out var file) && file.TryGetProperty("mimeType", out var mime) ? mime.GetString() : null,
                        item.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0,
                        item.TryGetProperty("lastModifiedDateTime", out var modified) && modified.TryGetDateTimeOffset(out var when) ? when : null,
                        item.TryGetProperty("eTag", out var etag) ? etag.GetString() : null,
                        folder.DriveId));
                }

                url = document.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
            }
        }

        return new ConnectorListing(files, EmptyIsAuthoritative: false);
    }

    public async Task<byte[]> DownloadAsync(string accessToken, RemoteFile file, CancellationToken cancellationToken = default)
    {
        var root = string.IsNullOrWhiteSpace(file.DriveId) ? $"{Graph}/me/drive" : $"{Graph}/drives/{Uri.EscapeDataString(file.DriveId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/items/{Uri.EscapeDataString(file.ExternalId)}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Send(request, cancellationToken);
        Throw(response.StatusCode, isRoot: false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes;
    }

    private async Task<JsonDocument> GetJsonAsync(string accessToken, string url, bool isRoot, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Send(request, cancellationToken);
        Throw(response.StatusCode, isRoot);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClientFactory.CreateClient(UserFileService.HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ConnectorSourceException("provider_error", "Microsoft could not be reached.");
        }
    }

    private static string DriveRoot(ConnectorFolder folder) =>
        string.IsNullOrWhiteSpace(folder.DriveId) ? $"{Graph}/me/drive" : $"{Graph}/drives/{Uri.EscapeDataString(folder.DriveId)}";

    private static void Throw(HttpStatusCode status, bool isRoot)
    {
        if ((int)status is >= 200 and < 300) return;
        throw status switch
        {
            HttpStatusCode.Unauthorized => new ConnectorSourceException("sign_in_expired", "Microsoft no longer accepts the sign-in this connector reads with."),
            HttpStatusCode.NotFound or HttpStatusCode.Forbidden => new ConnectorSourceException("folder_unavailable", isRoot
                ? "The folder was moved or deleted, or this account can no longer open it."
                : "The file was moved or deleted, or this account can no longer open it."),
            _ => new ConnectorSourceException("provider_error", "Microsoft could not complete the request. It will be tried again."),
        };
    }
}

/// <summary>
/// Reads the Google Drive files a person picked. The permission IIP asks Google for is the narrow one that covers only
/// files the person chose, so it cannot list a folder: each connector entry is one picked file. Google saying a picked
/// file is gone (not found, or in the bin) is a positive answer, so that, unlike an empty folder listing, does remove it.
/// </summary>
public sealed class GoogleDriveFileSource(IHttpClientFactory httpClientFactory) : IConnectorFileSource
{
    private const string Drive = "https://www.googleapis.com/drive/v3";

    // Google Docs, Sheets and Slides have no file to download as they are; only plain files that IIP can read are taken.
    private static readonly HashSet<string> Readable = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/markdown",
    };

    public FileProvider Provider => FileProvider.Google;

    public async Task<ConnectorListing> ListAsync(string accessToken, ConnectorFolder folder, CancellationToken cancellationToken = default)
    {
        if (!folder.IsFile)
        {
            throw new ConnectorSourceException("folder_unavailable", "Google Drive files are chosen one at a time in Google's own picker. Choose the files instead of the folder.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{Drive}/files/{Uri.EscapeDataString(folder.FolderId)}?supportsAllDrives=true&fields=id,name,mimeType,size,modifiedTime,md5Checksum,trashed");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Send(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ConnectorListing([], EmptyIsAuthoritative: true);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || IsSignInProblem(body))
        {
            throw new ConnectorSourceException("sign_in_expired", "Google no longer accepts the sign-in this connector reads with.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new ConnectorSourceException("folder_unavailable", "This Google account can no longer open the file.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ConnectorSourceException("provider_error", "Google could not complete the request. It will be tried again.");
        }

        using var document = JsonDocument.Parse(body);
        var file = document.RootElement;
        if (file.TryGetProperty("trashed", out var trashed) && trashed.ValueKind == JsonValueKind.True)
        {
            return new ConnectorListing([], EmptyIsAuthoritative: true);
        }

        var mime = file.TryGetProperty("mimeType", out var m) ? m.GetString() : null;
        if (mime is null || !Readable.Contains(mime))
        {
            throw new ConnectorSourceException("folder_unavailable", "This kind of file cannot be read. Pick a PDF, Word document or text file.");
        }

        return new ConnectorListing(
        [
            new RemoteFile(
                folder.FolderId,
                file.TryGetProperty("name", out var name) ? name.GetString() ?? folder.FolderId : folder.FolderId,
                folder.FolderId,
                mime,
                file.TryGetProperty("size", out var size) && long.TryParse(size.GetString(), out var bytes) ? bytes : 0,
                file.TryGetProperty("modifiedTime", out var modified) && modified.TryGetDateTimeOffset(out var when) ? when : null,
                file.TryGetProperty("md5Checksum", out var md5) ? md5.GetString() : null),
        ], EmptyIsAuthoritative: true);
    }

    public async Task<byte[]> DownloadAsync(string accessToken, RemoteFile file, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Drive}/files/{Uri.EscapeDataString(file.ExternalId)}?alt=media&supportsAllDrives=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Send(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ConnectorSourceException("sign_in_expired", "Google no longer accepts the sign-in this connector reads with.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ConnectorSourceException("provider_error", "The file could not be read from Google.");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClientFactory.CreateClient(UserFileService.HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ConnectorSourceException("provider_error", "Google could not be reached.");
        }
    }

    // An expired or under-scoped sign-in must fail the run, not look like the file was removed.
    private static bool IsSignInProblem(string body) =>
        body.Contains("insufficientPermissions", StringComparison.OrdinalIgnoreCase)
        || body.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.OrdinalIgnoreCase)
        || body.Contains("SERVICE_DISABLED", StringComparison.OrdinalIgnoreCase)
        || body.Contains("accessNotConfigured", StringComparison.OrdinalIgnoreCase);
}
