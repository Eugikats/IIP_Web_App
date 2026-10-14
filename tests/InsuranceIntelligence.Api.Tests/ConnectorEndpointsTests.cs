using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Connectors;
using InsuranceIntelligence.Application.Connectors;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Application.Knowledge;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Connectors;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The admin surface for connectors: who may use it, what it accepts, and that removing something takes effect at once.</summary>
public sealed class ConnectorEndpointsTests
{
    private static readonly ConnectorEndpoints.FolderInput Folder = new("folder-1", "Policy wordings", null, "Motor", "PolicyWording", false);
    private static readonly ConnectorEndpoints.FolderInput PickedFile = new("file-1", "Wording.pdf", null, "Motor", "PolicyWording", true);

    [Fact]
    public async Task AnAdministratorWhoHasConnectedTheirAccountCanCreateAConnector_AndItIsAudited()
    {
        var rig = new Rig();

        var result = await rig.Create("microsoft", Folder);

        Assert.Equal(StatusCodes.Status201Created, Status(result));
        var stored = Assert.Single(await rig.Connectors.ListAsync(Rig.Scope));
        Assert.Equal(("user-admin", FileProvider.Microsoft), (stored.OwnerUserId, stored.Provider));
        Assert.Contains(rig.Audit.Events, e => e.ActionName == "connector.created");
    }

    [Fact]
    public async Task WithoutTheirOwnConnectedAccount_ACannotCreateAConnector_AndIsToldToConnectFirst()
    {
        var rig = new Rig { Connected = false };

        var result = await rig.Create("microsoft", Folder);

        Assert.Equal(StatusCodes.Status409Conflict, Status(result));
        Assert.Empty(await rig.Connectors.ListAsync(Rig.Scope));
        Assert.Contains("Connect your own", Body(result).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("google", false)]
    [InlineData("microsoft", true)]
    public async Task GoogleTakesPickedFilesOnly_AndMicrosoftFoldersOnly(string provider, bool isFile)
    {
        var rig = new Rig();
        var wrong = Folder with { IsFile = isFile };

        var result = await rig.Create(provider, wrong);

        Assert.Equal(StatusCodes.Status400BadRequest, Status(result));
        Assert.Empty(await rig.Connectors.ListAsync(Rig.Scope));
    }

    [Theory]
    [InlineData("", "Policy wordings", "Motor", "PolicyWording")]
    [InlineData("folder-1", "", "Motor", "PolicyWording")]
    [InlineData("folder-1", "Policy wordings", "", "PolicyWording")]
    [InlineData("folder-1", "Policy wordings", "Motor", "Nonsense")]
    public async Task EveryEntryNeedsAnIdANameAProductLineAndADocumentType(string id, string name, string line, string type)
    {
        var rig = new Rig();
        var result = await rig.Create("microsoft", new ConnectorEndpoints.FolderInput(id, name, null, line, type, false));
        Assert.Equal(StatusCodes.Status400BadRequest, Status(result));
    }

    [Fact]
    public async Task APersonWithoutTheKnowledgePermissionCannotUseAnyOfIt()
    {
        var rig = new Rig { Role = "ClaimsOfficer" };

        Assert.Equal(StatusCodes.Status403Forbidden, Status(await rig.Create("microsoft", Folder)));
        Assert.Equal(StatusCodes.Status403Forbidden, Status(await rig.List()));
        Assert.Empty(await rig.Connectors.ListAsync(Rig.Scope));
    }

    [Fact]
    public async Task RemovingAFolderTakesItsFilesOutOfUseAtOnce_AndKeepsTheOthers()
    {
        var rig = new Rig();
        var second = Folder with { FolderId = "folder-2", DisplayName = "FAQs" };
        var connector = await rig.Seed(Folder, second);
        await rig.AddSyncedFile(connector, "folder-1", "a.txt");
        await rig.AddSyncedFile(connector, "folder-2", "b.txt");

        var result = await rig.ReplaceFolders(connector.ConnectorId, second);

        Assert.Equal(StatusCodes.Status200OK, Status(result));
        Assert.Equal(1, Body(result).GetProperty("retired").GetInt32());
        var documents = await rig.Knowledge.ListAsync(Rig.Scope);
        Assert.Single(documents, d => d.ApprovalStatus == KnowledgeDocumentStatus.Archived);
        Assert.Single(documents, d => d.ApprovalStatus == KnowledgeDocumentStatus.Draft);
        Assert.Contains(rig.Audit.Events, e => e.ActionName == "connector.folders_changed");
    }

    [Fact]
    public async Task DeletingAConnectorTakesEverythingItBroughtInOutOfUse()
    {
        var rig = new Rig();
        var connector = await rig.Seed(Folder);
        await rig.AddSyncedFile(connector, "folder-1", "a.txt");

        var result = await rig.Delete(connector.ConnectorId);

        Assert.Equal(StatusCodes.Status200OK, Status(result));
        Assert.Null(await rig.Connectors.GetAsync(Rig.Scope, connector.ConnectorId));
        Assert.All(await rig.Knowledge.ListAsync(Rig.Scope), d => Assert.Equal(KnowledgeDocumentStatus.Archived, d.ApprovalStatus));
        Assert.Empty(await rig.Items.ListAsync(Rig.Scope, connector.ConnectorId));
    }

    [Fact]
    public async Task AnotherWorkspacesConnectorIsNotFound_ForEveryAction()
    {
        var rig = new Rig();
        var theirs = new DataConnector
        {
            ConnectorId = Guid.NewGuid(), Scope = new TenantScope("tenant-b", "workspace-b"), Provider = FileProvider.Microsoft,
            OwnerUserId = "other", CreatedBy = "other", CreatedAt = DateTimeOffset.UtcNow, Folders = [new ConnectorFolder("x", "x", null, "Motor", KnowledgeDocumentType.Other)],
        };
        await rig.Connectors.SaveAsync(theirs);

        Assert.Equal(StatusCodes.Status404NotFound, Status(await rig.Delete(theirs.ConnectorId)));
        Assert.Equal(StatusCodes.Status404NotFound, Status(await rig.ReplaceFolders(theirs.ConnectorId, Folder)));
        Assert.Equal(StatusCodes.Status404NotFound, Status(await rig.SyncNow(theirs.ConnectorId)));
        Assert.Equal(StatusCodes.Status404NotFound, Status(await rig.Pause(theirs.ConnectorId)));
        Assert.NotNull(await rig.Connectors.GetAsync(theirs.Scope, theirs.ConnectorId));
        var list = Body(await rig.List()).GetProperty("connectors");
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task SyncNowRunsOnce_ThenAsksToWaitAMinute()
    {
        var rig = new Rig();
        var connector = await rig.Seed(Folder);
        rig.Source.Files = [new RemoteFile("a.txt", "a.txt", "folder-1", "text/plain", 20, DateTimeOffset.UtcNow, "v1")];

        var first = await rig.SyncNow(connector.ConnectorId);
        var second = await rig.SyncNow(connector.ConnectorId);

        Assert.Equal(StatusCodes.Status200OK, Status(first));
        Assert.Equal(1, Body(first).GetProperty("added").GetInt32());
        Assert.Equal(StatusCodes.Status429TooManyRequests, Status(second));
    }

    [Fact]
    public async Task APausedConnectorIsListedAsPaused_AndResumeSwitchesItBackOn()
    {
        var rig = new Rig();
        var connector = await rig.Seed(Folder);

        await rig.Pause(connector.ConnectorId);
        Assert.Equal("Paused", Body(await rig.List()).GetProperty("connectors")[0].GetProperty("statusLabel").GetString());

        await rig.Resume(connector.ConnectorId);
        Assert.Equal("Active", Body(await rig.List()).GetProperty("connectors")[0].GetProperty("statusLabel").GetString());
    }

    [Fact]
    public async Task ThePickedFileRuleHoldsForAnUpdateToo()
    {
        var rig = new Rig();
        var connector = await rig.Seed(Folder);
        Assert.Equal(StatusCodes.Status200OK, Status(await rig.ReplaceFolders(connector.ConnectorId, Folder)));
        Assert.Equal(StatusCodes.Status400BadRequest, Status(await rig.ReplaceFolders(connector.ConnectorId, PickedFile)));
    }

    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 200;

    private static JsonElement Body(IResult result) => JsonSerializer.SerializeToElement(((IValueHttpResult)result).Value);

    private sealed class Rig
    {
        public static readonly TenantScope Scope = new("tenant-demo", "workspace-operations");

        public string Role { get; init; } = "Admin";
        public bool Connected { get; init; } = true;
        public EfDataConnectorRepository Connectors { get; }
        public EfConnectorItemRepository Items { get; }
        public InMemoryKnowledgeDocumentRepository Knowledge { get; } = new();
        public FakeAudit Audit { get; } = new();
        public FakeSource Source { get; } = new();
        private readonly IConnectorRunRepository runs;
        private readonly KnowledgeIngestionService sink;
        private readonly ConnectorSyncService sync;
        private readonly TenantContextAccessor tenant = new();
        private readonly InMemoryAuditRepository auditRepository = new();

        public Rig()
        {
            var db = new InsuranceIntelligenceDbContext(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Connectors = new EfDataConnectorRepository(db);
            Items = new EfConnectorItemRepository(db);
            runs = new EfConnectorRunRepository(db);
            sink = new KnowledgeIngestionService(Knowledge, new TextStorage(), auditRepository, new InMemoryKnowledgeIndexProvider());
            sync = new ConnectorSyncService(new Tokens(this), [Source], Items, runs, Connectors, sink, auditRepository, TimeProvider.System);
        }

        public bool IsConnected => Connected;

        private (DefaultHttpContext Http, InfrastructureOptions Options) Context()
        {
            var http = new DefaultHttpContext { TraceIdentifier = "trace-1" };
            http.Request.Headers["X-Tenant-Id"] = Scope.TenantId;
            http.Request.Headers["X-Workspace-Id"] = Scope.WorkspaceId;
            http.Request.Headers["X-User-Id"] = "user-admin";
            http.Request.Headers["x-demo-user-id"] = "user-admin";
            http.Request.Headers["x-demo-role"] = Role;
            tenant.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, "user-admin", [Role]));
            return (http, new InfrastructureOptions { AuthMode = "Development" });
        }

        public Task<IResult> Create(string provider, params ConnectorEndpoints.FolderInput[] folders)
        {
            var (http, options) = Context();
            return ConnectorEndpoints.Create(new ConnectorEndpoints.CreateRequest(provider, folders), Connectors, new Tokens(this), new Files(), Audit, tenant, tenant, TimeProvider.System, options, http, default);
        }

        public Task<IResult> List()
        {
            var (http, options) = Context();
            return ConnectorEndpoints.List(Connectors, Items, runs, tenant, tenant, options, http, default);
        }

        public Task<IResult> ReplaceFolders(Guid id, params ConnectorEndpoints.FolderInput[] folders)
        {
            var (http, options) = Context();
            return ConnectorEndpoints.ReplaceFolders(id, new ConnectorEndpoints.FoldersRequest(folders), Connectors, sync, Audit, tenant, tenant, TimeProvider.System, options, http, default);
        }

        public Task<IResult> SyncNow(Guid id)
        {
            var (http, options) = Context();
            return ConnectorEndpoints.SyncNow(id, Connectors, sync, tenant, tenant, TimeProvider.System, options, http, default);
        }

        public Task<IResult> Pause(Guid id)
        {
            var (http, options) = Context();
            return ConnectorEndpoints.Pause(id, Connectors, Audit, tenant, tenant, TimeProvider.System, options, http, default);
        }

        public Task<IResult> Resume(Guid id)
        {
            var (http, options) = Context();
            return ConnectorEndpoints.Resume(id, Connectors, Audit, tenant, tenant, TimeProvider.System, options, http, default);
        }

        public Task<IResult> Delete(Guid id)
        {
            var (http, options) = Context();
            return ConnectorEndpoints.Delete(id, Connectors, Items, sync, Audit, tenant, tenant, TimeProvider.System, options, http, default);
        }

        public async Task<DataConnector> Seed(params ConnectorEndpoints.FolderInput[] folders)
        {
            var connector = new DataConnector
            {
                ConnectorId = Guid.NewGuid(), Scope = Scope, Provider = FileProvider.Microsoft, OwnerUserId = "user-admin", CreatedBy = "user-admin", CreatedAt = DateTimeOffset.UtcNow,
                Folders = folders.Select(f => new ConnectorFolder(f.FolderId!, f.DisplayName!, null, f.ProductLine!, KnowledgeDocumentType.PolicyWording, f.IsFile)).ToArray(),
            };
            await Connectors.SaveAsync(connector);
            return connector;
        }

        /// <summary>Brings one file in as the sync would, so there is something to take out of use.</summary>
        public async Task AddSyncedFile(DataConnector connector, string folderId, string name)
        {
            Source.Files = [new RemoteFile(name, name, folderId, "text/plain", 20, DateTimeOffset.UtcNow, "v1")];
            var only = connector with { Folders = connector.Folders.Where(f => f.FolderId == folderId).ToArray() };
            await sync.RunAsync(only, "seed");
        }

        private sealed class Tokens(Rig rig) : IFileAccessTokenProvider
        {
            public Task<string> GetAccessTokenAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default) =>
                rig.IsConnected ? Task.FromResult("token") : Task.FromException<string>(new FileConnectionException("not_connected", "not connected"));
        }
    }

    private sealed class FakeSource : IConnectorFileSource
    {
        public IReadOnlyList<RemoteFile> Files { get; set; } = [];
        public FileProvider Provider => FileProvider.Microsoft;
        public Task<ConnectorListing> ListAsync(string accessToken, ConnectorFolder folder, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorListing(Files.Where(f => f.FolderId == folder.FolderId).ToArray(), false));
        public Task<byte[]> DownloadAsync(string accessToken, RemoteFile file, CancellationToken cancellationToken = default) =>
            Task.FromResult(Encoding.UTF8.GetBytes($"{file.Name} cover details and terms."));
    }

    private sealed class Files : IUserFileService
    {
        public Task<IReadOnlyList<FileConnectionStatus>> StatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FileConnectionStatus>>([new FileConnectionStatus("microsoft", true, true, "admin@example.test", null)]);
        public Task<FileConnectStart> StartAsync(FileProvider provider, string? returnTo, bool includeSharePoint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileConnectResult> ExchangeAsync(FileProvider provider, string code, string state, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DisconnectAsync(FileProvider provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileBrowseResult> BrowseAsync(FileProvider provider, string? folderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DownloadedFile> DownloadAsync(FileProvider provider, string fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GooglePickerSession> PickerSessionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SavedFile> UploadAsync(FileProvider provider, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetAccessTokenAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TextStorage : IKnowledgeStorageProvider
    {
        public async Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8);
            return new KnowledgeStorageResult($"k/{request.KnowledgeDocumentId}", request.ContentType, request.FileSizeBytes, await reader.ReadToEndAsync(cancellationToken));
        }
    }

    private sealed class FakeAudit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}
