using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Infrastructure.Connectors;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>A connector, its runs and the files it has seen belong to one workspace and never show in another.</summary>
public sealed class ConnectorRepositoryTests
{
    private static readonly TenantScope Ours = new("tenant-a", "workspace-a");
    private static readonly TenantScope Theirs = new("tenant-b", "workspace-b");

    [Fact]
    public async Task AConnectorIsOnlyVisibleToItsOwnWorkspace()
    {
        await using var db = Db();
        var repository = new EfDataConnectorRepository(db);
        var connector = Connector(Ours);
        await repository.SaveAsync(connector);

        Assert.NotNull(await repository.GetAsync(Ours, connector.ConnectorId));
        Assert.Null(await repository.GetAsync(Theirs, connector.ConnectorId));
        Assert.Single(await repository.ListAsync(Ours));
        Assert.Empty(await repository.ListAsync(Theirs));
    }

    [Fact]
    public async Task AnotherWorkspaceCannotOverwriteOrDeleteAConnector()
    {
        await using var db = Db();
        var repository = new EfDataConnectorRepository(db);
        var connector = Connector(Ours);
        await repository.SaveAsync(connector);

        await Assert.ThrowsAsync<TenantIsolationException>(() => repository.SaveAsync(connector with { Scope = Theirs }));
        await repository.DeleteAsync(Theirs, connector.ConnectorId);

        Assert.NotNull(await repository.GetAsync(Ours, connector.ConnectorId));
    }

    [Fact]
    public async Task TheScheduledSyncSeesOnlyActiveConnectors_EachWithItsOwnScope()
    {
        await using var db = Db();
        var repository = new EfDataConnectorRepository(db);
        var active = Connector(Ours);
        var paused = Connector(Theirs) with { Status = ConnectorStatus.Paused };
        var needsReconnect = Connector(Theirs) with { Status = ConnectorStatus.NeedsReconnect };
        var otherActive = Connector(Theirs);
        foreach (var connector in new[] { active, paused, needsReconnect, otherActive }) await repository.SaveAsync(connector);

        var due = await repository.ListActiveAcrossWorkspacesAsync();

        Assert.Equal(2, due.Count);
        Assert.Contains(due, c => c.ConnectorId == active.ConnectorId && c.Scope == Ours);
        Assert.Contains(due, c => c.ConnectorId == otherActive.ConnectorId && c.Scope == Theirs);
    }

    [Fact]
    public async Task RunsAreListedNewestFirst_ForTheirOwnConnectorAndWorkspaceOnly()
    {
        await using var db = Db();
        var repository = new EfConnectorRunRepository(db);
        var connectorId = Guid.NewGuid();
        await repository.SaveAsync(Run(Ours, connectorId, ConnectorRunOutcome.Failed));
        await Task.Delay(5);
        await repository.SaveAsync(Run(Ours, connectorId, ConnectorRunOutcome.Succeeded));
        await repository.SaveAsync(Run(Ours, Guid.NewGuid(), ConnectorRunOutcome.Succeeded));

        var runs = await repository.ListAsync(Ours, connectorId, 10);

        Assert.Equal(2, runs.Count);
        Assert.Equal(ConnectorRunOutcome.Succeeded, runs[0].Outcome);
        Assert.Empty(await repository.ListAsync(Theirs, connectorId, 10));
    }

    [Fact]
    public async Task ItemsAreFoundByConnectorAndFileId_AndDeletingAConnectorsItemsLeavesOthers()
    {
        await using var db = Db();
        var repository = new EfConnectorItemRepository(db);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await repository.SaveAsync(Item(Ours, first, "file-1"));
        await repository.SaveAsync(Item(Ours, first, "file-2"));
        await repository.SaveAsync(Item(Ours, second, "file-1"));

        Assert.NotNull(await repository.GetAsync(Ours, first, "file-1"));
        Assert.Null(await repository.GetAsync(Theirs, first, "file-1"));
        Assert.Equal(2, (await repository.ListAsync(Ours, first)).Count);

        await repository.DeleteForConnectorAsync(Theirs, first);
        Assert.Equal(2, (await repository.ListAsync(Ours, first)).Count);

        await repository.DeleteForConnectorAsync(Ours, first);
        Assert.Empty(await repository.ListAsync(Ours, first));
        Assert.Single(await repository.ListAsync(Ours, second));
    }

    private static InsuranceIntelligenceDbContext Db() =>
        new(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DataConnector Connector(TenantScope scope) => new()
    {
        ConnectorId = Guid.NewGuid(),
        Scope = scope,
        Provider = FileProvider.Google,
        OwnerUserId = "user-1",
        CreatedBy = "user-1",
        CreatedAt = DateTimeOffset.UtcNow,
        Folders = [new ConnectorFolder("folder-1", "Policy wordings", null, "Motor", KnowledgeDocumentType.PolicyWording)],
    };

    private static ConnectorRun Run(TenantScope scope, Guid connectorId, ConnectorRunOutcome outcome) => new()
    {
        RunId = Guid.NewGuid(),
        ConnectorId = connectorId,
        Scope = scope,
        StartedAt = DateTimeOffset.UtcNow,
        FinishedAt = DateTimeOffset.UtcNow,
        Outcome = outcome,
    };

    private static ConnectorItem Item(TenantScope scope, Guid connectorId, string externalId) => new()
    {
        ItemId = ConnectorItem.StorageKey(connectorId, externalId),
        ConnectorId = connectorId,
        Scope = scope,
        ExternalId = externalId,
        FolderId = "folder-1",
        FileName = $"{externalId}.pdf",
        LastSeenAt = DateTimeOffset.UtcNow,
    };
}
