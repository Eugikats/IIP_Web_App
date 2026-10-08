using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.Extensions.Configuration;

namespace InsuranceIntelligence.Infrastructure.Connections;

/// <summary>
/// The platform's app registrations for the file providers. Present only when the platform operator has
/// set them up; until then every provider reports "not configured" and no sign-in is offered.
/// Secrets come from configuration (Key Vault in a deployment) and are never sent to a browser.
/// </summary>
public sealed record FileProviderOptions(
    string? GoogleClientId,
    string? GoogleClientSecret,
    string? GoogleAppId,
    string? GoogleDeveloperKey,
    string? MicrosoftClientId,
    string? MicrosoftClientSecret,
    string? RedirectUri)
{
    public bool GoogleConfigured => Has(GoogleClientId) && Has(GoogleClientSecret) && Has(RedirectUri);

    public bool MicrosoftConfigured => Has(MicrosoftClientId) && Has(MicrosoftClientSecret) && Has(RedirectUri);

    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);

    public static FileProviderOptions From(IConfiguration configuration) => new(
        configuration["Files:Google:ClientId"],
        configuration["Files:Google:ClientSecret"],
        configuration["Files:Google:AppId"],
        configuration["Files:Google:DeveloperKey"],
        configuration["Files:Microsoft:ClientId"],
        configuration["Files:Microsoft:ClientSecret"],
        configuration["Files:RedirectUri"]);
}

public sealed class UserFileService(
    IHttpClientFactory httpClientFactory,
    FileProviderOptions options,
    IConnectionSecretProtector protector,
    IUserFileConnectionRepository repository,
    ITenantContextAccessor tenantContextAccessor,
    IAuditWriter auditWriter) : IUserFileService
{
    public const string HttpClientName = "user-files";
    public const long MaxDownloadBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private const string GoogleAuthorize = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleToken = "https://oauth2.googleapis.com/token";
    private const string GoogleDrive = "https://www.googleapis.com/drive/v3";
    private const string MicrosoftAuthorize = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    private const string MicrosoftToken = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    private const string Graph = "https://graph.microsoft.com/v1.0";

    // Only files the person picks (or IIP creates) are visible to IIP in Google Drive.
    private const string GoogleScopes = "openid email https://www.googleapis.com/auth/drive.file";
    private const string MicrosoftScopes = "offline_access openid email User.Read Files.ReadWrite";
    private const string MicrosoftSharePointScope = "Sites.Selected";

    public async Task<IReadOnlyList<FileConnectionStatus>> StatusAsync(CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var statuses = new List<FileConnectionStatus>();
        foreach (var provider in new[] { FileProvider.Google, FileProvider.Microsoft })
        {
            var configured = IsConfigured(provider);
            var connection = configured ? await repository.GetAsync(context.Scope, context.UserId, provider, cancellationToken) : null;
            statuses.Add(new FileConnectionStatus(
                Key(provider),
                configured,
                connection is not null,
                connection?.AccountEmail,
                configured ? null : $"{Name(provider)} sign-in has not been set up for IIP yet."));
        }

        return statuses;
    }

    public Task<FileConnectStart> StartAsync(FileProvider provider, string? returnTo, bool includeSharePoint, CancellationToken cancellationToken = default)
    {
        RequireConfigured(provider);
        var context = tenantContextAccessor.Current;
        var state = protector.Protect(JsonSerializer.Serialize(new StatePayload(
            context.UserId, context.TenantId, context.WorkspaceId, Key(provider), SafeReturnTo(returnTo), DateTimeOffset.UtcNow.Add(StateLifetime), Guid.NewGuid().ToString("N"))));

        var query = provider == FileProvider.Google
            ? new Dictionary<string, string>
            {
                ["client_id"] = options.GoogleClientId!,
                ["redirect_uri"] = options.RedirectUri!,
                ["response_type"] = "code",
                ["scope"] = GoogleScopes,
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["include_granted_scopes"] = "true",
                ["state"] = state,
            }
            : new Dictionary<string, string>
            {
                ["client_id"] = options.MicrosoftClientId!,
                ["redirect_uri"] = options.RedirectUri!,
                ["response_type"] = "code",
                ["response_mode"] = "query",
                ["scope"] = includeSharePoint ? $"{MicrosoftScopes} {MicrosoftSharePointScope}" : MicrosoftScopes,
                ["prompt"] = "select_account",
                ["state"] = state,
            };

        var url = (provider == FileProvider.Google ? GoogleAuthorize : MicrosoftAuthorize)
            + "?" + string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return Task.FromResult(new FileConnectStart(url));
    }

    public async Task<FileConnectResult> ExchangeAsync(FileProvider provider, string code, string state, CancellationToken cancellationToken = default)
    {
        RequireConfigured(provider);
        var context = tenantContextAccessor.Current;
        var payload = ReadState(state);

        // The sign-in must come back to the same person, workspace and provider that started it.
        if (payload is null
            || payload.Expires < DateTimeOffset.UtcNow
            || !string.Equals(payload.UserId, context.UserId, StringComparison.OrdinalIgnoreCase)
            || payload.TenantId != context.TenantId
            || payload.WorkspaceId != context.WorkspaceId
            || payload.Provider != Key(provider))
        {
            throw new FileConnectionException("bad_state", "This sign-in link has expired or belongs to someone else. Start the connection again.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new FileConnectionException("provider_error", $"{Name(provider)} did not return a sign-in code.");
        }

        var token = await RequestTokenAsync(provider, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = options.RedirectUri!,
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new FileConnectionException("provider_error", $"{Name(provider)} did not allow IIP to stay connected. Try again and accept every permission asked for.");
        }

        var email = EmailFromIdToken(token.IdToken);
        await repository.SaveAsync(new UserFileConnection
        {
            Scope = context.Scope,
            UserId = context.UserId,
            Provider = provider,
            AccountEmail = email,
            ProtectedRefreshToken = protector.Protect(token.RefreshToken),
            GrantedScopes = token.Scope ?? string.Empty,
            ConnectedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);

        await AuditAsync("file_connection.connected", Key(provider), $"{Name(provider)} account connected.", cancellationToken);
        return new FileConnectResult(Key(provider), email, payload.ReturnTo);
    }

    public async Task DisconnectAsync(FileProvider provider, CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        await repository.DeleteAsync(context.Scope, context.UserId, provider, cancellationToken);
        await AuditAsync("file_connection.disconnected", Key(provider), $"{Name(provider)} account disconnected.", cancellationToken);
    }

    public async Task<FileBrowseResult> BrowseAsync(FileProvider provider, string? folderId, CancellationToken cancellationToken = default)
    {
        if (provider != FileProvider.Microsoft)
        {
            throw new FileConnectionException("not_supported", "Google Drive files are chosen in Google's own file picker.");
        }

        var access = await AccessTokenAsync(provider, cancellationToken);
        var path = string.IsNullOrWhiteSpace(folderId)
            ? $"{Graph}/me/drive/root/children"
            : $"{Graph}/me/drive/items/{Uri.EscapeDataString(folderId)}/children";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{path}?$select=id,name,size,folder,file,lastModifiedDateTime&$top=100&$orderby=name");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, provider, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var items = new List<FileItem>();
        foreach (var item in document.RootElement.GetProperty("value").EnumerateArray())
        {
            var isFolder = item.TryGetProperty("folder", out _);
            items.Add(new FileItem(
                item.GetProperty("id").GetString() ?? string.Empty,
                item.GetProperty("name").GetString() ?? string.Empty,
                isFolder,
                item.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : null,
                item.TryGetProperty("file", out var file) && file.TryGetProperty("mimeType", out var mime) ? mime.GetString() : null,
                item.TryGetProperty("lastModifiedDateTime", out var modified) && modified.TryGetDateTimeOffset(out var when) ? when : null));
        }

        return new FileBrowseResult(folderId, items.OrderByDescending(i => i.IsFolder).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<DownloadedFile> DownloadAsync(FileProvider provider, string fileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId) || fileId.Length > 300)
        {
            throw new FileConnectionException("bad_request", "Choose a file first.");
        }

        var access = await AccessTokenAsync(provider, cancellationToken);
        var client = httpClientFactory.CreateClient(HttpClientName);

        string name;
        string url;
        if (provider == FileProvider.Microsoft)
        {
            using var meta = await SendAsync(client, access, $"{Graph}/me/drive/items/{Uri.EscapeDataString(fileId)}?$select=id,name,size,folder", cancellationToken);
            await EnsureSuccessAsync(meta, provider, cancellationToken);
            using var doc = JsonDocument.Parse(await meta.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.TryGetProperty("folder", out _))
            {
                throw new FileConnectionException("bad_request", "That is a folder. Choose a file.");
            }

            name = doc.RootElement.GetProperty("name").GetString() ?? "file";
            RequireSmallEnough(doc.RootElement.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : null);
            url = $"{Graph}/me/drive/items/{Uri.EscapeDataString(fileId)}/content";
        }
        else
        {
            using var meta = await SendAsync(client, access, $"{GoogleDrive}/files/{Uri.EscapeDataString(fileId)}?fields=id,name,mimeType,size", cancellationToken);
            await EnsureSuccessAsync(meta, provider, cancellationToken);
            using var doc = JsonDocument.Parse(await meta.Content.ReadAsStringAsync(cancellationToken));
            name = doc.RootElement.GetProperty("name").GetString() ?? "file";
            var mimeType = doc.RootElement.TryGetProperty("mimeType", out var m) ? m.GetString() : null;
            if (mimeType == "application/vnd.google-apps.spreadsheet")
            {
                // A Google Sheet is read as CSV.
                name = name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? name : name + ".csv";
                url = $"{GoogleDrive}/files/{Uri.EscapeDataString(fileId)}/export?mimeType=text%2Fcsv";
            }
            else
            {
                RequireSmallEnough(doc.RootElement.TryGetProperty("size", out var s) && long.TryParse(s.GetString(), out var size) ? size : null);
                url = $"{GoogleDrive}/files/{Uri.EscapeDataString(fileId)}?alt=media";
            }
        }

        using var response = await SendAsync(client, access, url, cancellationToken);
        await EnsureSuccessAsync(response, provider, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxDownloadBytes)
            {
                throw new FileConnectionException("too_large", "That file is larger than 5 MB. Split the list and try again.");
            }
        }

        await AuditAsync("file_connection.file_read", Key(provider), $"A file was read from {Name(provider)} for an import.", cancellationToken);
        return new DownloadedFile(name, buffer.ToArray(), response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    public async Task<GooglePickerSession> PickerSessionAsync(CancellationToken cancellationToken = default)
    {
        var access = await AccessTokenAsync(FileProvider.Google, cancellationToken);
        var context = tenantContextAccessor.Current;
        var connection = await repository.GetAsync(context.Scope, context.UserId, FileProvider.Google, cancellationToken);
        if (string.IsNullOrWhiteSpace(options.GoogleAppId))
        {
            throw new FileConnectionException("not_configured", "Google's file picker has not been set up for IIP yet.");
        }

        return new GooglePickerSession(access, options.GoogleAppId, options.GoogleDeveloperKey, connection?.AccountEmail);
    }

    private async Task<string> AccessTokenAsync(FileProvider provider, CancellationToken cancellationToken)
    {
        RequireConfigured(provider);
        var context = tenantContextAccessor.Current;
        var connection = await repository.GetAsync(context.Scope, context.UserId, provider, cancellationToken)
            ?? throw new FileConnectionException("not_connected", $"Connect your {Name(provider)} account first.");

        string refreshToken;
        try
        {
            refreshToken = protector.Unprotect(connection.ProtectedRefreshToken);
        }
        catch (Exception)
        {
            // The key ring changed, so the saved connection can no longer be read. Ask for a fresh sign-in.
            await repository.DeleteAsync(context.Scope, context.UserId, provider, cancellationToken);
            throw new FileConnectionException("not_connected", $"Your {Name(provider)} connection needs to be set up again.");
        }

        TokenResponse token;
        try
        {
            token = await RequestTokenAsync(provider, new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            }, cancellationToken);
        }
        catch (FileConnectionException ex) when (ex.Code == "invalid_grant")
        {
            // The person revoked access (or it expired). The saved connection is no longer any use.
            await repository.DeleteAsync(context.Scope, context.UserId, provider, cancellationToken);
            throw new FileConnectionException("not_connected", $"{Name(provider)} no longer allows IIP to read your files. Connect your account again.");
        }

        // Microsoft rotates the refresh token on use; keep the newest one.
        if (!string.IsNullOrWhiteSpace(token.RefreshToken) && token.RefreshToken != refreshToken)
        {
            await repository.SaveAsync(connection with { ProtectedRefreshToken = protector.Protect(token.RefreshToken) }, cancellationToken);
        }

        return token.AccessToken ?? throw new FileConnectionException("provider_error", $"{Name(provider)} did not return access.");
    }

    private async Task<TokenResponse> RequestTokenAsync(FileProvider provider, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        form["client_id"] = provider == FileProvider.Google ? options.GoogleClientId! : options.MicrosoftClientId!;
        form["client_secret"] = provider == FileProvider.Google ? options.GoogleClientSecret! : options.MicrosoftClientSecret!;
        if (provider == FileProvider.Microsoft && form["grant_type"] == "refresh_token")
        {
            form["scope"] = MicrosoftScopes;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, provider == FileProvider.Google ? GoogleToken : MicrosoftToken)
        {
            Content = new FormUrlEncodedContent(form),
        };
        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // The provider's own error text is not shown to anyone: it can carry request details.
            var code = "provider_error";
            try
            {
                using var error = JsonDocument.Parse(body);
                if (error.RootElement.TryGetProperty("error", out var e) && e.GetString() == "invalid_grant")
                {
                    code = "invalid_grant";
                }
            }
            catch (JsonException)
            {
                // Not JSON: keep the generic code.
            }

            throw new FileConnectionException(code, $"{Name(provider)} could not confirm the sign-in. Try connecting again.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new TokenResponse(
            root.TryGetProperty("access_token", out var a) ? a.GetString() : null,
            root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null,
            root.TryGetProperty("id_token", out var i) ? i.GetString() : null,
            root.TryGetProperty("scope", out var s) ? s.GetString() : null);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string accessToken, string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response, FileProvider provider, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return Task.CompletedTask;
        }

        var code = (int)response.StatusCode switch
        {
            401 or 403 => "not_connected",
            404 => "not_found",
            _ => "provider_error",
        };
        var message = code switch
        {
            "not_connected" => $"{Name(provider)} did not allow that. Connect your account again, or choose a file you can open.",
            "not_found" => "That file or folder was not found.",
            _ => $"{Name(provider)} could not be reached just now. Try again in a moment.",
        };
        throw new FileConnectionException(code, message);
    }

    private static void RequireSmallEnough(long? size)
    {
        if (size is > MaxDownloadBytes)
        {
            throw new FileConnectionException("too_large", "That file is larger than 5 MB. Split the list and try again.");
        }
    }

    private bool IsConfigured(FileProvider provider) => provider == FileProvider.Google ? options.GoogleConfigured : options.MicrosoftConfigured;

    private void RequireConfigured(FileProvider provider)
    {
        if (!IsConfigured(provider))
        {
            throw new FileConnectionException("not_configured", $"{Name(provider)} sign-in has not been set up for IIP yet.");
        }
    }

    /// <summary>Only a path inside IIP can be returned to after sign-in, so a crafted link cannot send a person elsewhere.</summary>
    private static string SafeReturnTo(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("/app", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains("\\")
            ? value
            : "/app";

    private StatePayload? ReadState(string state)
    {
        try
        {
            return JsonSerializer.Deserialize<StatePayload>(protector.Unprotect(state));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? EmailFromIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            foreach (var name in new[] { "email", "preferred_username" })
            {
                if (doc.RootElement.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text)
                {
                    return text;
                }
            }
        }
        catch (Exception)
        {
            // An unreadable id token only means no account name is shown.
        }

        return null;
    }

    private Task AuditAsync(string action, string provider, string reason, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        return auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, CaseId: null, DocumentId: null, UserId: context.UserId, ActorType.User, action,
                DateTimeOffset.UtcNow, BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: provider,
                Confidence: null, ValidationResult: "recorded", ApprovalId: null, AuditReason: reason, CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);
    }

    private static string Key(FileProvider provider) => provider == FileProvider.Google ? "google" : "microsoft";

    private static string Name(FileProvider provider) => provider == FileProvider.Google ? "Google Drive" : "OneDrive";

    private sealed record TokenResponse(string? AccessToken, string? RefreshToken, string? IdToken, string? Scope);

    private sealed record StatePayload(string UserId, string TenantId, string WorkspaceId, string Provider, string ReturnTo, DateTimeOffset Expires, string Nonce);
}
