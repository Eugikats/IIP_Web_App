using System.Net;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Infrastructure.Connectors;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The provider sources against canned answers from Microsoft Graph and Google Drive. Not yet run against real accounts:
/// that needs the owner's registrations. What is pinned here is how each provider answer is understood, in particular
/// which answers mean "sign in again" and which mean "this is gone".
/// </summary>
public sealed class ProviderFileSourceTests
{
    private static readonly ConnectorFolder Folder = new("folder-1", "Wordings", null, "Motor", KnowledgeDocumentType.PolicyWording);

    [Fact]
    public async Task Microsoft_WalksSubfolders_ReadsPages_AndCarriesVersionAndDrive()
    {
        var http = new Stub();
        http.Respond("https://graph.microsoft.com/v1.0/drives/drive-9/items/folder-1/children", new
        {
            value = new object[]
            {
                new { id = "f1", name = "motor.pdf", size = 1200, file = new { mimeType = "application/pdf" }, lastModifiedDateTime = "2026-10-01T10:00:00Z", eTag = "\"etag-1\"" },
                new { id = "sub", name = "Archive", folder = new { childCount = 1 } },
            },
            @odata_nextLink = (string?)null,
        });
        http.Respond("https://graph.microsoft.com/v1.0/drives/drive-9/items/sub/children", new
        {
            value = new object[] { new { id = "f2", name = "old.docx", size = 800, file = new { mimeType = "application/msword" }, lastModifiedDateTime = "2026-09-01T10:00:00Z", eTag = "\"etag-2\"" } },
        });

        var listing = await new MicrosoftGraphFileSource(http.Factory).ListAsync("token", Folder with { DriveId = "drive-9" });

        Assert.False(listing.EmptyIsAuthoritative);
        Assert.Equal(["f1", "f2"], listing.Files.Select(f => f.ExternalId).Order());
        Assert.All(listing.Files, f => Assert.Equal("folder-1", f.FolderId));
        Assert.All(listing.Files, f => Assert.Equal("drive-9", f.DriveId));
        Assert.Equal("\"etag-1\"", listing.Files.Single(f => f.ExternalId == "f1").Version);
        Assert.All(http.Requests, request => Assert.Equal("Bearer token", request.Authorization));
    }

    [Fact]
    public async Task Microsoft_FollowsNextLinkPages()
    {
        var http = new Stub();
        http.Respond("https://graph.microsoft.com/v1.0/me/drive/items/folder-1/children?$select", new
        {
            value = new object[] { new { id = "f1", name = "a.txt", size = 1, file = new { mimeType = "text/plain" } } },
            odata = 1,
        }, nextLink: "https://graph.microsoft.com/v1.0/page-2");
        http.Respond("https://graph.microsoft.com/v1.0/page-2", new { value = new object[] { new { id = "f2", name = "b.txt", size = 1, file = new { mimeType = "text/plain" } } } });

        var listing = await new MicrosoftGraphFileSource(http.Factory).ListAsync("token", Folder);

        Assert.Equal(2, listing.Files.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "sign_in_expired")]
    [InlineData(HttpStatusCode.NotFound, "folder_unavailable")]
    [InlineData(HttpStatusCode.Forbidden, "folder_unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "provider_error")]
    [InlineData(HttpStatusCode.TooManyRequests, "provider_error")]
    public async Task Microsoft_AnswersAreUnderstood_AndMessagesCarryNoProviderBody(HttpStatusCode status, string code)
    {
        var http = new Stub();
        http.Respond("https://graph.microsoft.com", new { error = new { code = "secret-provider-detail" } }, status);

        var failure = await Assert.ThrowsAsync<ConnectorSourceException>(() => new MicrosoftGraphFileSource(http.Factory).ListAsync("token", Folder));

        Assert.Equal(code, failure.Code);
        Assert.DoesNotContain("secret-provider-detail", failure.Message);
        Assert.DoesNotContain("token", failure.Message);
    }

    [Fact]
    public async Task Microsoft_AFolderThatListsEmpty_IsNotAuthoritative()
    {
        var http = new Stub();
        http.Respond("https://graph.microsoft.com", new { value = Array.Empty<object>() });
        var listing = await new MicrosoftGraphFileSource(http.Factory).ListAsync("token", Folder);
        Assert.Empty(listing.Files);
        Assert.False(listing.EmptyIsAuthoritative);
    }

    [Fact]
    public async Task Microsoft_AFolderEntryThatIsAFile_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<ConnectorSourceException>(() => new MicrosoftGraphFileSource(new Stub().Factory).ListAsync("token", Folder with { IsFile = true }));
        Assert.Equal("folder_unavailable", failure.Code);
    }

    [Fact]
    public async Task Microsoft_Download_ReturnsTheBytes_UsingTheFilesDrive()
    {
        var http = new Stub();
        http.RespondBytes("https://graph.microsoft.com/v1.0/drives/drive-9/items/f1/content", Encoding.UTF8.GetBytes("wording"));

        var bytes = await new MicrosoftGraphFileSource(http.Factory).DownloadAsync("token", new RemoteFile("f1", "a.txt", "folder-1", null, 7, null, null, "drive-9"));

        Assert.Equal("wording", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Google_APickedFileThatIsThere_IsListedWithItsChecksum()
    {
        var http = new Stub();
        http.Respond("https://www.googleapis.com/drive/v3/files/file-9", new { id = "file-9", name = "Wording.pdf", mimeType = "application/pdf", size = "2048", modifiedTime = "2026-10-01T10:00:00Z", md5Checksum = "abc123" });

        var listing = await new GoogleDriveFileSource(http.Factory).ListAsync("token", new ConnectorFolder("file-9", "Wording.pdf", null, "Motor", KnowledgeDocumentType.PolicyWording, IsFile: true));

        var file = Assert.Single(listing.Files);
        Assert.Equal(("file-9", "Wording.pdf", "abc123", 2048L), (file.ExternalId, file.Name, file.Version, file.Size));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Google_APickedFileThatIsNotFound_IsAnAuthoritativeEmptyAnswer(HttpStatusCode status)
    {
        var http = new Stub();
        http.Respond("https://www.googleapis.com/drive/v3/files/file-9", new { error = new { message = "File not found" } }, status);

        var listing = await new GoogleDriveFileSource(http.Factory).ListAsync("token", Picked());

        Assert.Empty(listing.Files);
        Assert.True(listing.EmptyIsAuthoritative);
    }

    [Fact]
    public async Task Google_AFileInTheBin_IsGone()
    {
        var http = new Stub();
        http.Respond("https://www.googleapis.com/drive/v3/files/file-9", new { id = "file-9", name = "x.pdf", mimeType = "application/pdf", trashed = true });

        var listing = await new GoogleDriveFileSource(http.Factory).ListAsync("token", Picked());

        Assert.Empty(listing.Files);
        Assert.True(listing.EmptyIsAuthoritative);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}", "sign_in_expired")]
    [InlineData(HttpStatusCode.Forbidden, "{\"error\":{\"errors\":[{\"reason\":\"insufficientPermissions\"}]}}", "sign_in_expired")]
    [InlineData(HttpStatusCode.Forbidden, "{\"error\":{\"message\":\"The user does not have access\"}}", "folder_unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "{}", "provider_error")]
    public async Task Google_SignInProblemsFailTheRun_NeverLookLikeARemoval(HttpStatusCode status, string body, string code)
    {
        var http = new Stub();
        http.RespondRaw("https://www.googleapis.com/drive/v3/files/file-9", body, status);

        var failure = await Assert.ThrowsAsync<ConnectorSourceException>(() => new GoogleDriveFileSource(http.Factory).ListAsync("token", Picked()));

        Assert.Equal(code, failure.Code);
        Assert.DoesNotContain("insufficientPermissions", failure.Message);
    }

    [Fact]
    public async Task Google_AFolderOrADocsFile_CannotBeSynced()
    {
        var folder = await Assert.ThrowsAsync<ConnectorSourceException>(() => new GoogleDriveFileSource(new Stub().Factory).ListAsync("token", Picked() with { IsFile = false }));
        Assert.Equal("folder_unavailable", folder.Code);

        var http = new Stub();
        http.Respond("https://www.googleapis.com/drive/v3/files/file-9", new { id = "file-9", name = "Notes", mimeType = "application/vnd.google-apps.document" });
        var doc = await Assert.ThrowsAsync<ConnectorSourceException>(() => new GoogleDriveFileSource(http.Factory).ListAsync("token", Picked()));
        Assert.Contains("PDF, Word document or text file", doc.Message);
    }

    private static ConnectorFolder Picked() => new("file-9", "Wording", null, "Motor", KnowledgeDocumentType.PolicyWording, IsFile: true);

    private sealed class Stub : HttpMessageHandler
    {
        private readonly List<(string Prefix, Func<HttpResponseMessage> Make)> routes = [];

        public List<(string Uri, string? Authorization)> Requests { get; } = [];

        public IHttpClientFactory Factory => new FactoryOf(this);

        public void Respond(string prefix, object body, HttpStatusCode status = HttpStatusCode.OK, string? nextLink = null) =>
            routes.Insert(0, (prefix, () =>
            {
                var json = JsonSerializer.Serialize(body).Replace("@odata_nextLink", "@odata.nextLink");
                if (nextLink is not null) json = json.TrimEnd('}') + $",\"@odata.nextLink\":\"{nextLink}\"}}";
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }));

        public void RespondRaw(string prefix, string body, HttpStatusCode status) =>
            routes.Insert(0, (prefix, () => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

        public void RespondBytes(string prefix, byte[] bytes) =>
            routes.Insert(0, (prefix, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((uri, request.Headers.Authorization?.ToString()));
            var route = routes.FirstOrDefault(r => uri.StartsWith(r.Prefix, StringComparison.Ordinal));
            return Task.FromResult(route.Make is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : route.Make());
        }
    }

    private sealed class FactoryOf(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
