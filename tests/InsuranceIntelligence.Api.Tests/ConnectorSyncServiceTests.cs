using System.Text;
using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.Connectors;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Application.Knowledge;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Infrastructure.Connectors;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The rules that make a connector safe to leave running. Each is a thing that must hold when a provider misbehaves,
/// a sign-in lapses or a folder changes, because the alternative is knowledge quietly disappearing or going stale.
/// </summary>
public sealed class ConnectorSyncServiceTests
{
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");
    private static readonly ConnectorFolder Wordings = new("folder-1", "Policy wordings", null, "Motor", KnowledgeDocumentType.PolicyWording);
    private static readonly ConnectorFolder BankFeed = new("folder-events", "Bank events", null, "Bank events", KnowledgeDocumentType.Other, Purpose: ConnectorFolderPurpose.BankEvents);
    private static readonly ConnectorFolder Faqs = new("folder-2", "FAQs", null, "General", KnowledgeDocumentType.CustomerFaq);

    [Fact]
    public async Task FirstRun_BringsFilesInAsPendingDrafts_WithUnknownSensitivity_AndNothingApproved()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "Motor cover pays for repairs.", "v1"), Remote("b.txt", "Excess applies.", "v1"));

        var run = await rig.SyncAsync();

        Assert.Equal(ConnectorRunOutcome.Succeeded, run.Outcome);
        Assert.Equal((2, 2), (run.Found, run.Added));
        var documents = await rig.Documents();
        Assert.Equal(2, documents.Count);
        Assert.All(documents, d =>
        {
            Assert.Equal(KnowledgeDocumentStatus.Draft, d.ApprovalStatus);
            Assert.Equal(KnowledgeSensitivity.Unknown, d.Sensitivity);
            Assert.Equal(KnowledgeIndexedStatus.NotIndexed, d.IndexedStatus);
            Assert.Equal("Motor", d.ProductLine);
            Assert.Equal(KnowledgeDocumentType.PolicyWording, d.DocumentType);
        });
    }

    [Fact]
    public async Task SecondRun_WithNothingChanged_DoesNotIngestAgain()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "Motor cover.", "v1"));
        await rig.SyncAsync();

        var second = await rig.SyncAsync();

        Assert.Equal((1, 0, 0), (second.Found, second.Added, second.Updated));
        Assert.Single(await rig.Documents());
        Assert.Equal(1, rig.Source.Downloads);
    }

    [Fact]
    public async Task AChangedFile_BecomesANewPendingDocument_AndTheApprovedOldOneIsTakenOutOfUse()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "Excess is 100,000.", "v1"));
        await rig.SyncAsync();
        var original = (await rig.Documents()).Single();
        await rig.Knowledge.SaveAsync(original with { ApprovalStatus = KnowledgeDocumentStatus.Approved, Sensitivity = KnowledgeSensitivity.Approved });
        await rig.Index.IndexAsync((await rig.Knowledge.GetAsync(original.KnowledgeDocumentId, Scope))!);

        rig.Source.Set(Wordings, Remote("a.txt", "Excess is 250,000.", "v2"));
        var run = await rig.SyncAsync();

        Assert.Equal((0, 1), (run.Added, run.Updated));
        var documents = await rig.Documents();
        Assert.Equal(2, documents.Count);
        Assert.Equal(KnowledgeDocumentStatus.Archived, documents.Single(d => d.KnowledgeDocumentId == original.KnowledgeDocumentId).ApprovalStatus);
        var replacement = documents.Single(d => d.KnowledgeDocumentId != original.KnowledgeDocumentId);
        Assert.Equal(KnowledgeDocumentStatus.Draft, replacement.ApprovalStatus);
        Assert.Equal(KnowledgeSensitivity.Unknown, replacement.Sensitivity);
        Assert.False(rig.Index.IsIndexed(original.KnowledgeDocumentId));
    }

    [Fact]
    public async Task AFileThatLeavesAFolderThatReadsProperly_IsTakenOutOfUseAtOnce()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"), Remote("b.txt", "Two.", "v1"));
        await rig.SyncAsync();

        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));
        var run = await rig.SyncAsync();

        Assert.Equal(1, run.Removed);
        var documents = await rig.Documents();
        Assert.Single(documents, d => d.ApprovalStatus == KnowledgeDocumentStatus.Archived);
        Assert.Equal(ConnectorItemState.Removed, (await rig.Items.GetAsync(Scope, rig.Connector.ConnectorId, "b.txt"))!.State);
    }

    [Fact]
    public async Task AnExpiredSignIn_FailsTheRun_AndRemovesNothing()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));
        await rig.SyncAsync();

        rig.Tokens.Failure = new FileConnectionException("sign_in_expired", "revoked");
        var run = await rig.SyncAsync();

        Assert.Equal(ConnectorRunOutcome.SignInExpired, run.Outcome);
        Assert.Equal(ConnectorStatus.NeedsReconnect, (await rig.Connectors.GetAsync(Scope, rig.Connector.ConnectorId))!.Status);
        Assert.DoesNotContain(await rig.Documents(), d => d.ApprovalStatus == KnowledgeDocumentStatus.Archived);
        Assert.Equal(ConnectorItemState.Pending, (await rig.Items.GetAsync(Scope, rig.Connector.ConnectorId, "a.txt"))!.State);
        Assert.DoesNotContain("revoked", run.SafeError);
    }

    [Fact]
    public async Task ASignInThatExpiresPartWayThrough_RemovesNothingFromAnyFolder()
    {
        var rig = await Rig.CreateAsync(Wordings, Faqs);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));
        rig.Source.Set(Faqs, Remote("f.txt", "Faq.", "v1", Faqs));
        await rig.SyncAsync();

        rig.Source.Set(Wordings);
        rig.Source.Throw(Faqs, new ConnectorSourceException("sign_in_expired", "The sign-in has expired."));
        var run = await rig.SyncAsync();

        Assert.Equal(ConnectorRunOutcome.SignInExpired, run.Outcome);
        Assert.DoesNotContain(await rig.Documents(), d => d.ApprovalStatus == KnowledgeDocumentStatus.Archived);
    }

    [Fact]
    public async Task AFolderThatSuddenlyListsEmpty_RemovesNothing_AndSaysWhy()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"), Remote("b.txt", "Two.", "v1"));
        await rig.SyncAsync();

        rig.Source.Set(Wordings);
        var run = await rig.SyncAsync();

        Assert.Equal(ConnectorRunOutcome.Partial, run.Outcome);
        Assert.Equal(0, run.Removed);
        Assert.Contains("listed as empty", run.SafeError);
        Assert.DoesNotContain(await rig.Documents(), d => d.ApprovalStatus == KnowledgeDocumentStatus.Archived);
    }

    [Fact]
    public async Task AFolderThatCannotBeRead_KeepsItsFiles_AndTheOtherFolderStillSyncs()
    {
        var rig = await Rig.CreateAsync(Wordings, Faqs);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));
        rig.Source.Set(Faqs, Remote("f.txt", "Faq.", "v1", Faqs));
        await rig.SyncAsync();

        rig.Source.Throw(Wordings, new ConnectorSourceException("folder_unavailable", "That folder cannot be read now."));
        rig.Source.Set(Faqs, Remote("f.txt", "Faq.", "v1", Faqs), Remote("g.txt", "More.", "v1", Faqs));
        var run = await rig.SyncAsync();

        Assert.Equal(ConnectorRunOutcome.Partial, run.Outcome);
        Assert.Equal(1, run.Added);
        Assert.DoesNotContain(await rig.Documents(), d => d.ApprovalStatus == KnowledgeDocumentStatus.Archived);
        Assert.Equal(ConnectorItemState.Pending, (await rig.Items.GetAsync(Scope, rig.Connector.ConnectorId, "a.txt"))!.State);
    }

    [Fact]
    public async Task APickedFileTheProviderSaysIsGone_IsRemoved_BecauseThatEmptyAnswerIsAuthoritative()
    {
        var picked = new ConnectorFolder("file-9", "Wording.txt", null, "Motor", KnowledgeDocumentType.PolicyWording, IsFile: true);
        var rig = await Rig.CreateAsync(picked);
        rig.Source.Set(picked, Remote("file-9", "Cover.", "v1", picked) with { Name = "Wording.txt" });
        await rig.SyncAsync();

        rig.Source.Set(picked, authoritativeEmpty: true);
        var run = await rig.SyncAsync();

        Assert.Equal(1, run.Removed);
        Assert.Equal(ConnectorRunOutcome.Succeeded, run.Outcome);
    }

    [Fact]
    public async Task UnsupportedAndOversizedFilesAreSkippedWithAReason_AndNotChurnedEveryRun()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings,
            Remote("photo.exe", "x", "v1"),
            Remote("huge.txt", "x", "v1") with { Size = 11 * 1024 * 1024 },
            Remote("ok.txt", "Fine.", "v1"));

        var first = await rig.SyncAsync();
        await rig.SyncAsync();

        Assert.Equal((3, 1, 2), (first.Found, first.Added, first.Skipped));
        Assert.Single(await rig.Documents());
        var skipped = (await rig.Items.GetAsync(Scope, rig.Connector.ConnectorId, "photo.exe"))!;
        Assert.Equal(ConnectorItemState.Skipped, skipped.State);
        Assert.Contains("Unsupported", skipped.SkipReason);
    }

    [Fact]
    public async Task OneFileThatCannotBeDownloaded_DoesNotStopTheOthers_AndIsRetriedNextRun()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"), Remote("b.txt", "Two.", "v1"));
        rig.Source.FailDownload("a.txt");

        var first = await rig.SyncAsync();
        Assert.Equal((1, 1, ConnectorRunOutcome.Partial), (first.Added, first.Failed, first.Outcome));

        rig.Source.FailDownload(null);
        var second = await rig.SyncAsync();
        Assert.Equal((1, 0, ConnectorRunOutcome.Succeeded), (second.Added, second.Failed, second.Outcome));
        Assert.Equal(2, (await rig.Documents()).Count);
    }

    [Fact]
    public async Task RemovingAFolderTakesItsFilesOutOfUseImmediately_AndLeavesOtherFolders()
    {
        var rig = await Rig.CreateAsync(Wordings, Faqs);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));
        rig.Source.Set(Faqs, Remote("f.txt", "Faq.", "v1", Faqs));
        await rig.SyncAsync();

        var retired = await rig.Service.RetireFoldersAsync(rig.Connector, [Wordings.FolderId], "Folder removed.", "corr");

        Assert.Equal(1, retired);
        var documents = await rig.Documents();
        Assert.Single(documents, d => d.ApprovalStatus == KnowledgeDocumentStatus.Archived);
        Assert.Single(documents, d => d.ApprovalStatus == KnowledgeDocumentStatus.Draft);
    }

    [Fact]
    public async Task WhatASyncCreatesBelongsToTheConnectorsWorkspaceOnly()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));
        await rig.SyncAsync();

        Assert.Single(await rig.Knowledge.ListAsync(Scope));
        Assert.Empty(await rig.Knowledge.ListAsync(new TenantScope("tenant-b", "workspace-b")));
        Assert.Empty(await rig.Items.ListAsync(new TenantScope("tenant-b", "workspace-b"), rig.Connector.ConnectorId));
    }

    [Fact]
    public async Task APausedConnectorStaysPausedAfterARunGivenByHand()
    {
        var rig = await Rig.CreateAsync(Wordings);
        await rig.Connectors.SaveAsync(rig.Connector with { Status = ConnectorStatus.Paused });
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));

        await rig.Service.RunAsync((await rig.Connectors.GetAsync(Scope, rig.Connector.ConnectorId))!, "corr");

        Assert.Equal(ConnectorStatus.Paused, (await rig.Connectors.GetAsync(Scope, rig.Connector.ConnectorId))!.Status);
    }

    [Fact]
    public async Task AFolderChangeMadeWhileARunIsInProgress_IsNotUndoneByTheRunsOwnSave()
    {
        var rig = await Rig.CreateAsync(Wordings);
        rig.Source.Set(Wordings, Remote("a.txt", "One.", "v1"));
        rig.Source.OnList = () => rig.Connectors.SaveAsync(rig.Connector with { Folders = [Wordings, Faqs] }).GetAwaiter().GetResult();

        await rig.SyncAsync();

        var stored = (await rig.Connectors.GetAsync(Scope, rig.Connector.ConnectorId))!;
        Assert.Equal(2, stored.Folders.Count);
        Assert.NotNull(stored.LastRunAt);
    }

    // ---- Folders of bank events

    private sealed class RecordingEventSink : IBankEventFileSink
    {
        public List<string> Files { get; } = [];
        public string? FailOn { get; set; }

        public Task<EventImportResult> ImportAsync(TenantScope scope, string fileName, byte[] content, string actorId, CancellationToken cancellationToken = default)
        {
            if (fileName == FailOn)
            {
                return Task.FromException<EventImportResult>(new EventException("The file needs a column for phone."));
            }

            Files.Add(fileName);
            return Task.FromResult(new EventImportResult(3, 2, 1, [], []));
        }
    }

    [Fact]
    public async Task AnEventFolder_HandsItsCsvToTheEventReader_NeverMakesKnowledge_AndReadsAChangedFileOnceMore()
    {
        var sink = new RecordingEventSink();
        var rig = await Rig.CreateAsync(sink, BankFeed);
        rig.Source.Set(BankFeed, Remote("events-27-oct.csv", "x", "v1", BankFeed));

        var first = await rig.SyncAsync();
        var second = await rig.SyncAsync();

        Assert.Equal(ConnectorRunOutcome.Succeeded, first.Outcome);
        Assert.Equal(1, first.Added);
        Assert.Equal(0, second.Added);
        Assert.Equal(["events-27-oct.csv"], sink.Files);
        Assert.Empty(await rig.Documents());
        var item = Assert.Single(await rig.Items.ListAsync(Scope, rig.Connector.ConnectorId));
        Assert.Equal(ConnectorItemState.Imported, item.State);
        Assert.Contains("2 new events", item.SkipReason);

        rig.Source.Set(BankFeed, Remote("events-27-oct.csv", "x", "v2", BankFeed));
        var third = await rig.SyncAsync();
        Assert.Equal(1, third.Updated);
        Assert.Equal(2, sink.Files.Count);
    }

    [Fact]
    public async Task AnEventFolder_SkipsFilesThatAreNotCsv_WithAReason()
    {
        var sink = new RecordingEventSink();
        var rig = await Rig.CreateAsync(sink, BankFeed);
        rig.Source.Set(BankFeed, Remote("notes.pdf", "x", "v1", BankFeed));

        var run = await rig.SyncAsync();

        Assert.Equal(1, run.Skipped);
        Assert.Empty(sink.Files);
        var item = Assert.Single(await rig.Items.ListAsync(Scope, rig.Connector.ConnectorId));
        Assert.Equal(ConnectorItemState.Skipped, item.State);
        Assert.Contains("CSV", item.SkipReason);
    }

    [Fact]
    public async Task AnEventFolder_WithoutAnEventReader_SaysSoInsteadOfPretending()
    {
        var rig = await Rig.CreateAsync(BankFeed);
        rig.Source.Set(BankFeed, Remote("events.csv", "x", "v1", BankFeed));

        var run = await rig.SyncAsync();

        Assert.Equal(1, run.Skipped);
        Assert.Contains("not available", (await rig.Items.ListAsync(Scope, rig.Connector.ConnectorId)).Single().SkipReason);
    }

    [Fact]
    public async Task AFileTheEventReaderRefuses_IsReportedAsFailed_AndTheOtherFilesStillLoad()
    {
        var sink = new RecordingEventSink { FailOn = "bad.csv" };
        var rig = await Rig.CreateAsync(sink, BankFeed);
        rig.Source.Set(BankFeed, Remote("bad.csv", "x", "v1", BankFeed), Remote("good.csv", "x", "v1", BankFeed));

        var run = await rig.SyncAsync();

        Assert.Equal(ConnectorRunOutcome.Partial, run.Outcome);
        Assert.Equal((1, 1), (run.Added, run.Failed));
        Assert.Equal(["good.csv"], sink.Files);
    }

    [Fact]
    public async Task EventsAlreadyReadAreNotTakenBackWhenTheFileLeavesTheFolder()
    {
        var sink = new RecordingEventSink();
        var rig = await Rig.CreateAsync(sink, BankFeed);
        rig.Source.Set(BankFeed, Remote("events.csv", "x", "v1", BankFeed));
        await rig.SyncAsync();

        rig.Source.Set(BankFeed, authoritativeEmpty: true);
        var run = await rig.SyncAsync();

        Assert.Equal(0, run.Removed);
        Assert.Equal(ConnectorItemState.Imported, (await rig.Items.ListAsync(Scope, rig.Connector.ConnectorId)).Single().State);
    }

    private static RemoteFile Remote(string name, string content, string version, ConnectorFolder? folder = null) =>
        new(name, name, (folder ?? Wordings).FolderId, "text/plain", Encoding.UTF8.GetByteCount(content), new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), version);

    private sealed class Rig
    {
        public required DataConnector Connector { get; init; }
        public required ConnectorSyncService Service { get; init; }
        public required FakeSource Source { get; init; }
        public required FakeTokens Tokens { get; init; }
        public required EfDataConnectorRepository Connectors { get; init; }
        public required EfConnectorItemRepository Items { get; init; }
        public required InMemoryKnowledgeDocumentRepository Knowledge { get; init; }
        public required InMemoryKnowledgeIndexProvider Index { get; init; }

        public static Task<Rig> CreateAsync(params ConnectorFolder[] folders) => CreateAsync(null, folders);

        public static async Task<Rig> CreateAsync(IBankEventFileSink? events, params ConnectorFolder[] folders)
        {
            var db = new InsuranceIntelligenceDbContext(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var connectors = new EfDataConnectorRepository(db);
            var items = new EfConnectorItemRepository(db);
            var runs = new EfConnectorRunRepository(db);
            var knowledge = new InMemoryKnowledgeDocumentRepository();
            var index = new InMemoryKnowledgeIndexProvider();
            var audit = new InMemoryAuditRepository();
            var sink = new KnowledgeIngestionService(knowledge, new TextStorage(), audit, index);
            var source = new FakeSource();
            var tokens = new FakeTokens();
            var connector = new DataConnector
            {
                ConnectorId = Guid.NewGuid(), Scope = Scope, Provider = FileProvider.Microsoft, OwnerUserId = "owner-1", CreatedBy = "owner-1",
                CreatedAt = DateTimeOffset.UtcNow, Folders = folders,
            };
            await connectors.SaveAsync(connector);
            return new Rig
            {
                Connector = connector, Source = source, Tokens = tokens, Connectors = connectors, Items = items, Knowledge = knowledge, Index = index,
                Service = new ConnectorSyncService(tokens, [source], items, runs, connectors, sink, audit, TimeProvider.System, events),
            };
        }

        public Task<ConnectorRun> SyncAsync() => Service.RunAsync(Connector, "corr");

        public async Task<IReadOnlyList<KnowledgeDocument>> Documents() => (await Knowledge.ListAsync(Scope)).ToArray();
    }

    private sealed class FakeTokens : IFileAccessTokenProvider
    {
        public FileConnectionException? Failure { get; set; }

        public Task<string> GetAccessTokenAsync(TenantScope scope, string userId, FileProvider provider, CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult("token") : Task.FromException<string>(Failure);
    }

    private sealed class FakeSource : IConnectorFileSource
    {
        private readonly Dictionary<string, (List<RemoteFile> Files, bool Authoritative, ConnectorSourceException? Throws)> folders = [];
        private readonly Dictionary<string, string> contents = [];
        private string? failDownload;

        public FileProvider Provider => FileProvider.Microsoft;
        public int Downloads { get; private set; }
        public Action? OnList { get; set; }

        public void Set(ConnectorFolder folder, params RemoteFile[] files) => folders[folder.FolderId] = ([.. files], false, null);

        public void Set(ConnectorFolder folder, bool authoritativeEmpty) => folders[folder.FolderId] = ([], authoritativeEmpty, null);

        public void Throw(ConnectorFolder folder, ConnectorSourceException exception) => folders[folder.FolderId] = ([], false, exception);

        public void FailDownload(string? externalId) => failDownload = externalId;

        public Task<ConnectorListing> ListAsync(string accessToken, ConnectorFolder folder, CancellationToken cancellationToken = default)
        {
            OnList?.Invoke();
            var entry = folders[folder.FolderId];
            if (entry.Throws is not null) return Task.FromException<ConnectorListing>(entry.Throws);
            foreach (var file in entry.Files) contents[file.ExternalId] = $"{file.Name} {file.Version}. Cover details.";
            return Task.FromResult(new ConnectorListing(entry.Files, entry.Authoritative));
        }

        public Task<byte[]> DownloadAsync(string accessToken, RemoteFile file, CancellationToken cancellationToken = default)
        {
            if (file.ExternalId == failDownload) return Task.FromException<byte[]>(new ConnectorSourceException("provider_error", "The file could not be read."));
            Downloads++;
            return Task.FromResult(Encoding.UTF8.GetBytes(contents[file.ExternalId]));
        }
    }

    private sealed class TextStorage : IKnowledgeStorageProvider
    {
        public async Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8);
            return new KnowledgeStorageResult($"k/{request.KnowledgeDocumentId}", request.ContentType, request.FileSizeBytes, await reader.ReadToEndAsync(cancellationToken));
        }
    }
}
