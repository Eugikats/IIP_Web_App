using System.Net;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Connections;

namespace InsuranceIntelligence.Api.Tests;

public sealed class UserFileServiceTests
{
    private static readonly FileProviderOptions Configured = new(
        "google-client", "google-secret", "12345", "dev-key", "ms-client", "ms-secret", "https://app.example.test/app/connect/callback");

    private static string IdToken(string email)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"email\":\"{email}\"}}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"header.{payload}.signature";
    }

    [Fact]
    public async Task Without_an_app_registration_every_provider_says_not_configured_and_no_sign_in_is_offered()
    {
        var rig = new Rig(new FileProviderOptions(null, null, null, null, null, null, null));

        var statuses = await rig.Service.StatusAsync();

        Assert.All(statuses, status =>
        {
            Assert.False(status.Configured);
            Assert.False(status.Connected);
            Assert.Contains("has not been set up", status.Reason);
        });
        var error = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.StartAsync(FileProvider.Google, "/app", false));
        Assert.Equal("not_configured", error.Code);
    }

    [Fact]
    public async Task Google_sign_in_asks_only_for_files_the_person_picks_and_returns_only_inside_iip()
    {
        var rig = new Rig(Configured);

        var start = await rig.Service.StartAsync(FileProvider.Google, "https://evil.example/steal", false);

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", start.AuthorizeUrl);
        Assert.Contains("drive.file", Uri.UnescapeDataString(start.AuthorizeUrl));
        Assert.Contains("access_type=offline", start.AuthorizeUrl);
        Assert.DoesNotContain("google-secret", start.AuthorizeUrl);

        rig.Http.Respond("https://oauth2.googleapis.com/token", new { access_token = "a1", refresh_token = "r1", id_token = IdToken("aisha@example.test"), scope = "drive.file" });
        var state = Uri.UnescapeDataString(start.AuthorizeUrl.Split("state=")[1].Split('&')[0]);
        var result = await rig.Service.ExchangeAsync(FileProvider.Google, "the-code", state);

        Assert.Equal("aisha@example.test", result.AccountEmail);
        Assert.Equal("/app", result.ReturnTo);
        var status = (await rig.Service.StatusAsync()).Single(item => item.Provider == "google");
        Assert.True(status.Connected);
        Assert.Equal("aisha@example.test", status.AccountEmail);
        Assert.StartsWith("enc:", rig.Repository.All.Single().ProtectedRefreshToken);
        Assert.Contains(rig.Audit.Events, e => e.ActionName == "file_connection.connected");
    }

    [Fact]
    public async Task A_sign_in_started_by_one_person_cannot_be_finished_as_another()
    {
        var rig = new Rig(Configured);
        var start = await rig.Service.StartAsync(FileProvider.Microsoft, "/app/acquisition/import", false);
        var state = Uri.UnescapeDataString(start.AuthorizeUrl.Split("state=")[1].Split('&')[0]);

        rig.Context.UserId = "someone-else";
        var error = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.ExchangeAsync(FileProvider.Microsoft, "code", state));

        Assert.Equal("bad_state", error.Code);
        Assert.Empty(rig.Repository.All);
    }

    [Fact]
    public async Task An_unreadable_or_forged_state_is_refused()
    {
        var rig = new Rig(Configured);

        var error = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.ExchangeAsync(FileProvider.Google, "code", "not-a-real-state"));

        Assert.Equal("bad_state", error.Code);
    }

    [Fact]
    public async Task Connections_belong_to_the_person_so_a_colleague_is_not_connected()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Google);

        rig.Context.UserId = "colleague";
        var status = (await rig.Service.StatusAsync()).Single(item => item.Provider == "google");

        Assert.False(status.Connected);
        var error = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.DownloadAsync(FileProvider.Google, "file-1"));
        Assert.Equal("not_connected", error.Code);
    }

    [Fact]
    public async Task A_google_sheet_is_read_as_csv()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Google);
        rig.Http.Respond("https://oauth2.googleapis.com/token", new { access_token = "fresh" });
        rig.Http.Respond("https://www.googleapis.com/drive/v3/files/sheet-1?fields", new { id = "sheet-1", name = "Branch customers", mimeType = "application/vnd.google-apps.spreadsheet" });
        rig.Http.RespondText("https://www.googleapis.com/drive/v3/files/sheet-1/export", "Name,Phone\nAisha,0776123456\n", "text/csv");

        var file = await rig.Service.DownloadAsync(FileProvider.Google, "sheet-1");

        Assert.Equal("Branch customers.csv", file.Name);
        Assert.Equal("Name,Phone\nAisha,0776123456\n", Encoding.UTF8.GetString(file.Content));
        Assert.Contains(rig.Http.Requests, request => request.Uri.Contains("/export?mimeType=text%2Fcsv") && request.Authorization == "Bearer fresh");
    }

    [Fact]
    public async Task A_file_larger_than_five_megabytes_is_refused_before_it_is_downloaded()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Microsoft);
        rig.Http.Respond("https://login.microsoftonline.com/common/oauth2/v2.0/token", new { access_token = "fresh" });
        rig.Http.Respond("https://graph.microsoft.com/v1.0/me/drive/items/big?$select", new { id = "big", name = "huge.xlsx", size = 9_000_000 });

        var error = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.DownloadAsync(FileProvider.Microsoft, "big"));

        Assert.Equal("too_large", error.Code);
        Assert.DoesNotContain(rig.Http.Requests, request => request.Uri.EndsWith("/content"));
    }

    [Fact]
    public async Task When_the_person_has_revoked_access_the_saved_connection_is_dropped()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Google);
        rig.Http.Respond("https://oauth2.googleapis.com/token", new { error = "invalid_grant" }, HttpStatusCode.BadRequest);

        var error = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.DownloadAsync(FileProvider.Google, "file-1"));

        Assert.Equal("not_connected", error.Code);
        Assert.Empty(rig.Repository.All);
    }

    [Fact]
    public async Task A_background_sync_with_a_revoked_sign_in_fails_with_its_own_code_and_removes_nothing()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Google);
        rig.Http.Respond("https://oauth2.googleapis.com/token", new { error = "invalid_grant" }, HttpStatusCode.BadRequest);
        var scope = new InsuranceIntelligence.Domain.Common.TenantScope("tenant-demo", "workspace-operations");

        var error = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.GetAccessTokenAsync(scope, "user-1", FileProvider.Google));

        Assert.Equal("sign_in_expired", error.Code);
        Assert.Single(rig.Repository.All);
    }

    [Fact]
    public async Task A_background_sync_gets_a_token_only_for_the_person_it_names()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Google);
        rig.Http.Respond("https://oauth2.googleapis.com/token", new { access_token = "fresh" });
        var scope = new InsuranceIntelligence.Domain.Common.TenantScope("tenant-demo", "workspace-operations");

        Assert.Equal("fresh", await rig.Service.GetAccessTokenAsync(scope, "user-1", FileProvider.Google));
        var colleague = await Assert.ThrowsAsync<FileConnectionException>(() => rig.Service.GetAccessTokenAsync(scope, "user-2", FileProvider.Google));
        Assert.Equal("not_connected", colleague.Code);
        var otherOrganisation = await Assert.ThrowsAsync<FileConnectionException>(
            () => rig.Service.GetAccessTokenAsync(new InsuranceIntelligence.Domain.Common.TenantScope("tenant-other", "workspace-operations"), "user-1", FileProvider.Google));
        Assert.Equal("not_connected", otherOrganisation.Code);
    }

    [Fact]
    public async Task Microsoft_folders_list_first_and_the_newest_refresh_token_is_kept()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Microsoft);
        rig.Http.Respond("https://login.microsoftonline.com/common/oauth2/v2.0/token", new { access_token = "fresh", refresh_token = "rotated" });
        rig.Http.Respond("https://graph.microsoft.com/v1.0/me/drive/root/children", new
        {
            value = new object[]
            {
                new { id = "f1", name = "customers.xlsx", size = 1200, file = new { mimeType = "application/vnd.ms-excel" }, lastModifiedDateTime = "2026-10-01T10:00:00Z" },
                new { id = "d1", name = "Branch lists", folder = new { childCount = 3 } },
            },
        });

        var result = await rig.Service.BrowseAsync(FileProvider.Microsoft, null);

        Assert.Equal(["Branch lists", "customers.xlsx"], result.Items.Select(item => item.Name));
        Assert.True(result.Items[0].IsFolder);
        Assert.Equal("enc:rotated", rig.Repository.All.Single().ProtectedRefreshToken);
    }

    [Fact]
    public async Task Disconnecting_removes_the_connection_and_is_audited()
    {
        var rig = new Rig(Configured);
        await rig.ConnectAsync(FileProvider.Google);

        await rig.Service.DisconnectAsync(FileProvider.Google);

        Assert.Empty(rig.Repository.All);
        Assert.Contains(rig.Audit.Events, e => e.ActionName == "file_connection.disconnected");
    }

    private sealed class Rig
    {
        public FakeHttp Http { get; } = new();
        public Repo Repository { get; } = new();
        public Context Context { get; } = new();
        public Audit Audit { get; } = new();
        public UserFileService Service { get; }

        public Rig(FileProviderOptions options)
        {
            Service = new UserFileService(new Factory(Http), options, new FakeProtector(), Repository, Context, Audit);
        }

        public async Task ConnectAsync(FileProvider provider)
        {
            var start = await Service.StartAsync(provider, "/app", false);
            var state = Uri.UnescapeDataString(start.AuthorizeUrl.Split("state=")[1].Split('&')[0]);
            Http.Respond(
                provider == FileProvider.Google ? "https://oauth2.googleapis.com/token" : "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                new { access_token = "first", refresh_token = "refresh-1", id_token = IdToken("aisha@example.test") });
            await Service.ExchangeAsync(provider, "code", state);
            Http.Clear();
        }
    }

    private sealed class Factory(FakeHttp http) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(http);
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        private readonly List<(string Prefix, Func<HttpResponseMessage> Make)> routes = [];

        public List<(string Uri, string? Authorization)> Requests { get; } = [];

        public void Respond(string prefix, object body, HttpStatusCode status = HttpStatusCode.OK) =>
            routes.Insert(0, (prefix, () => new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") }));

        public void RespondText(string prefix, string text, string contentType) =>
            routes.Insert(0, (prefix, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, contentType) }));

        public void Clear()
        {
            routes.Clear();
            Requests.Clear();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((uri, request.Headers.Authorization?.ToString()));
            var route = routes.FirstOrDefault(r => uri.StartsWith(r.Prefix, StringComparison.Ordinal));
            return Task.FromResult(route.Make is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : route.Make());
        }
    }

    private sealed class FakeProtector : IConnectionSecretProtector
    {
        public string Protect(string plaintext) => "enc:" + plaintext;

        public string Unprotect(string ciphertext) =>
            ciphertext.StartsWith("enc:", StringComparison.Ordinal) ? ciphertext[4..] : throw new InvalidOperationException("not protected");
    }

    private sealed class Repo : IUserFileConnectionRepository
    {
        private readonly Dictionary<Guid, UserFileConnection> store = [];

        public IReadOnlyCollection<UserFileConnection> All => store.Values;

        public Task<UserFileConnection?> GetAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.GetValueOrDefault(UserFileConnection.StorageKey(scope, userId, provider)));

        public Task SaveAsync(UserFileConnection connection, CancellationToken cancellationToken = default)
        {
            store[UserFileConnection.StorageKey(connection.Scope, connection.UserId, connection.Provider)] = connection;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default)
        {
            store.Remove(UserFileConnection.StorageKey(scope, userId, provider));
            return Task.CompletedTask;
        }
    }

    private sealed class Context : ITenantContextAccessor
    {
        public string UserId { get; set; } = "user-1";

        public TenantContext Current => new("tenant-demo", "workspace-operations", UserId, ["Admin"]);

        public bool HasCurrent => true;
    }

    private sealed class Audit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
