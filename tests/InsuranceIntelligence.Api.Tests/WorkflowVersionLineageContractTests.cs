using System.Text.Json;
using InsuranceIntelligence.Api.AgentWorkflows;
using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Queue;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// RF-03: immutable workflow key+version lineage and run pinning.
/// </summary>
public sealed class WorkflowVersionLineageContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task DraftNewVersion_And_Publish_Create_Immutable_Key_Plus_Version_Lineage()
    {
        var definitions = new InMemoryDefinitionRepository();
        var audits = new RecordingAuditWriter();
        var service = CreateWorkflowService(definitions, runs: new InMemoryRunRepository(), audits: audits);
        var catalog = AgentWorkflowCatalog.Find("document-to-decision")!;

        var save = await AgentWorkflowEndpoints.SaveDraft(
            new SaveAgentWorkflowDraftRequest(
                null,
                "tenant-document-to-decision",
                "Tenant lineage v1",
                catalog.Description,
                catalog.BusinessReferenceTypes,
                catalog.Nodes,
                catalog.Edges),
            service,
            CancellationToken.None);
        var (saveStatus, v1Draft) = await ExecuteAsync<AgentWorkflowDefinition>(save);
        Assert.Equal(StatusCodes.Status201Created, saveStatus);
        Assert.Equal(1, v1Draft.Version);

        var publishV1 = await AgentWorkflowEndpoints.Publish(v1Draft.Key, service, CancellationToken.None);
        var (publishStatus, publishedV1) = await ExecuteAsync<AgentWorkflowDefinition>(publishV1);
        Assert.Equal(StatusCodes.Status200OK, publishStatus);
        Assert.Equal(AgentWorkflowDefinitionStatus.Published, publishedV1.Status);
        Assert.Contains(audits.Events, e => e.ActionName == "agent_workflow.published");

        var mutatePublished = await AgentWorkflowEndpoints.SaveDraft(
            new SaveAgentWorkflowDraftRequest(
                publishedV1.DefinitionId,
                publishedV1.Key,
                "Mutate published",
                catalog.Description,
                catalog.BusinessReferenceTypes,
                catalog.Nodes,
                catalog.Edges),
            service,
            CancellationToken.None);
        var (mutateStatus, mutateBody) = await ExecuteAsync<JsonElement>(mutatePublished);
        Assert.Equal(StatusCodes.Status400BadRequest, mutateStatus);
        Assert.Contains(
            "immutable",
            mutateBody.GetProperty("errors").GetProperty("definitionId")[0].GetString(),
            StringComparison.OrdinalIgnoreCase);

        var draftNew = await AgentWorkflowEndpoints.DraftNewVersion(publishedV1.Key, service, CancellationToken.None);
        var (draftNewStatus, v2Draft) = await ExecuteAsync<AgentWorkflowDefinition>(draftNew);
        Assert.Equal(StatusCodes.Status201Created, draftNewStatus);
        Assert.Equal(2, v2Draft.Version);
        Assert.NotEqual(publishedV1.DefinitionId, v2Draft.DefinitionId);
        Assert.Contains(audits.Events, e => e.ActionName == "agent_workflow.draft_new_version.created");

        var publishV2 = await AgentWorkflowEndpoints.Publish(v2Draft.Key, service, CancellationToken.None);
        var (publishV2Status, publishedV2) = await ExecuteAsync<AgentWorkflowDefinition>(publishV2);
        Assert.Equal(StatusCodes.Status200OK, publishV2Status);
        Assert.Equal(2, publishedV2.Version);
        Assert.Equal(AgentWorkflowDefinitionStatus.Published, publishedV2.Status);

        var list = await AgentWorkflowEndpoints.ListVersions(publishedV1.Key, service, CancellationToken.None);
        var (listStatus, versions) = await ExecuteAsync<AgentWorkflowDefinition[]>(list);
        Assert.Equal(StatusCodes.Status200OK, listStatus);
        Assert.Equal(2, versions.Length);
        Assert.Equal(1, versions[0].Version);
        Assert.Equal(2, versions[1].Version);

        var getV1 = await AgentWorkflowEndpoints.GetVersion(publishedV1.Key, 1, service, CancellationToken.None);
        var (getStatus, fetchedV1) = await ExecuteAsync<AgentWorkflowDefinition>(getV1);
        Assert.Equal(StatusCodes.Status200OK, getStatus);
        Assert.Equal(publishedV1.DefinitionId, fetchedV1.DefinitionId);
        Assert.Equal(1, fetchedV1.Version);
    }

    [Fact]
    public async Task Concurrent_Same_Key_And_Version_Is_Rejected()
    {
        var definitions = new InMemoryDefinitionRepository(enforceUniqueKeyVersion: true);
        var catalog = AgentWorkflowCatalog.Find("document-to-decision")!;
        var first = new AgentWorkflowDefinition(
            Guid.NewGuid(),
            Scope,
            "tenant-document-to-decision",
            "First",
            catalog.Description,
            0,
            1,
            AgentWorkflowDefinitionStatus.Draft,
            false,
            catalog.BusinessReferenceTypes,
            catalog.Nodes,
            catalog.Edges,
            DateTimeOffset.UtcNow,
            "user-claims-1",
            null,
            null);
        await definitions.SaveAsync(first);

        var collision = first with { DefinitionId = Guid.NewGuid(), Name = "Collision" };
        var exception = await Assert.ThrowsAsync<AgentWorkflowValidationException>(() => definitions.SaveAsync(collision));
        Assert.Contains("key and version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InFlight_Run_Stays_Pinned_After_Newer_Version_Is_Published()
    {
        var definitions = new InMemoryDefinitionRepository();
        var runs = new InMemoryRunRepository();
        var service = CreateWorkflowService(definitions, runs);
        var catalog = AgentWorkflowCatalog.Find("document-to-decision")!;

        var save = await AgentWorkflowEndpoints.SaveDraft(
            new SaveAgentWorkflowDraftRequest(
                null,
                "tenant-document-to-decision",
                "Pinned v1",
                catalog.Description,
                catalog.BusinessReferenceTypes,
                catalog.Nodes,
                catalog.Edges),
            service,
            CancellationToken.None);
        var (_, v1Draft) = await ExecuteAsync<AgentWorkflowDefinition>(save);
        var publishV1 = await AgentWorkflowEndpoints.Publish(v1Draft.Key, service, CancellationToken.None);
        var (_, publishedV1) = await ExecuteAsync<AgentWorkflowDefinition>(publishV1);

        var start = await AgentWorkflowEndpoints.StartRun(
            publishedV1.Key,
            new StartAgentWorkflowRequest("case", "case-pinned-001", "corr-rf03-pin"),
            service,
            CancellationToken.None);
        var (startStatus, run) = await ExecuteAsync<AgentWorkflowRun>(start);
        Assert.Equal(StatusCodes.Status202Accepted, startStatus);
        Assert.Equal(publishedV1.DefinitionId, run.DefinitionId);
        Assert.Equal(1, run.WorkflowVersion);

        var draftNew = await AgentWorkflowEndpoints.DraftNewVersion(publishedV1.Key, service, CancellationToken.None);
        var (_, v2Draft) = await ExecuteAsync<AgentWorkflowDefinition>(draftNew);
        var renamedNodes = v2Draft.Nodes
            .Select(node => node.Key.Equals("document-check", StringComparison.OrdinalIgnoreCase)
                ? node with { Key = "document-check-v2", Name = "Check document v2" }
                : node)
            .ToArray();
        var renamedEdges = v2Draft.Edges
            .Select(edge => edge with
            {
                SourceNodeKey = edge.SourceNodeKey.Equals("document-check", StringComparison.OrdinalIgnoreCase)
                    ? "document-check-v2"
                    : edge.SourceNodeKey,
                TargetNodeKey = edge.TargetNodeKey.Equals("document-check", StringComparison.OrdinalIgnoreCase)
                    ? "document-check-v2"
                    : edge.TargetNodeKey
            })
            .ToArray();
        var reviewNode = renamedNodes.First(node => node.Type == AgentWorkflowNodeType.HumanReview);
        renamedNodes = renamedNodes
            .Select(node => node.Key.Equals(reviewNode.Key, StringComparison.OrdinalIgnoreCase)
                ? node with
                {
                    Configuration = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["correctionTarget"] = "document-check-v2"
                    }
                }
                : node)
            .ToArray();

        var saveV2 = await AgentWorkflowEndpoints.SaveDraft(
            new SaveAgentWorkflowDraftRequest(
                v2Draft.DefinitionId,
                v2Draft.Key,
                "Pinned v2 renamed",
                v2Draft.Description,
                v2Draft.BusinessReferenceTypes,
                renamedNodes,
                renamedEdges),
            service,
            CancellationToken.None);
        var (_, savedV2) = await ExecuteAsync<AgentWorkflowDefinition>(saveV2);
        var publishV2 = await AgentWorkflowEndpoints.Publish(savedV2.Key, service, CancellationToken.None);
        var (publishV2Status, publishedV2) = await ExecuteAsync<AgentWorkflowDefinition>(publishV2);
        Assert.Equal(StatusCodes.Status200OK, publishV2Status);
        Assert.Equal(2, publishedV2.Version);
        Assert.Contains(publishedV2.Nodes, n => n.Key == "document-check-v2");
        Assert.DoesNotContain(publishedV2.Nodes, n => n.Key == "document-check");

        var complete = await AgentWorkflowEndpoints.CompleteNode(
            run.RunId,
            "document-check",
            new CompleteAgentWorkflowNodeRequest(
                "ready",
                "Document check completed against pinned v1 definition.",
                [new AgentEvidenceReference("document", "doc-1", "Invoice", null, null, 0.91m, null)],
                "AgentWorkflowEngine",
                "document-checker",
                "v1",
                null),
            service,
            CancellationToken.None);
        var (completeStatus, updatedRun) = await ExecuteAsync<AgentWorkflowRun>(complete);
        Assert.Equal(StatusCodes.Status200OK, completeStatus);
        Assert.Equal(publishedV1.DefinitionId, updatedRun.DefinitionId);
        Assert.Equal(1, updatedRun.WorkflowVersion);
        Assert.Contains(
            updatedRun.NodeRuns,
            node => node.NodeKey == "document-check" && node.Status == AgentWorkflowNodeRunStatus.Completed);
    }

    private static AgentWorkflowService CreateWorkflowService(
        InMemoryDefinitionRepository definitions,
        InMemoryRunRepository runs,
        IAuditWriter? audits = null) =>
        new(
            new FixedContext("user-claims-1"),
            new AllowPermissionService(),
            definitions,
            runs,
            new InMemoryWorkItemRepository(),
            new NoOpQueueDispatcher(),
            audits ?? new RecordingAuditWriter());

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default!
            : (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private sealed class FixedContext(string userId) : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-claims", userId, ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class AllowPermissionService : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpQueueDispatcher : IMessageQueueDispatcher
    {
        public Task DispatchAsync(QueueMessageEnvelope envelope, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryDefinitionRepository(bool enforceUniqueKeyVersion = false) : IAgentWorkflowDefinitionRepository
    {
        private readonly List<AgentWorkflowDefinition> _items = [];

        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowDefinition>>(_items.Where(i => i.Scope == scope).ToArray());

        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowDefinition>>(_items
                .Where(i => i.Scope == scope && string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Version)
                .ToArray());

        public Task<AgentWorkflowDefinition?> GetAsync(Guid definitionId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.DefinitionId == definitionId && i.Scope == scope));

        public Task<AgentWorkflowDefinition?> GetByKeyAndVersionAsync(string key, int version, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i =>
                i.Scope == scope
                && string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)
                && i.Version == version));

        public Task<AgentWorkflowDefinition?> GetDraftByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items
                .Where(i => i.Scope == scope
                    && string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)
                    && i.Status == AgentWorkflowDefinitionStatus.Draft)
                .OrderByDescending(i => i.Version)
                .FirstOrDefault());

        public Task<AgentWorkflowDefinition?> GetLatestPublishedByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items
                .Where(i => i.Scope == scope
                    && string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)
                    && i.Status == AgentWorkflowDefinitionStatus.Published)
                .OrderByDescending(i => i.Version)
                .FirstOrDefault());

        public Task<int> GetMaxVersionAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items
                .Where(i => i.Scope == scope && string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase))
                .Select(i => i.Version)
                .DefaultIfEmpty(0)
                .Max());

        public Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken cancellationToken = default)
        {
            if (enforceUniqueKeyVersion
                && _items.Any(i =>
                    i.DefinitionId != definition.DefinitionId
                    && i.Scope == definition.Scope
                    && string.Equals(i.Key, definition.Key, StringComparison.OrdinalIgnoreCase)
                    && i.Version == definition.Version))
            {
                throw new AgentWorkflowValidationException("key", "A workflow definition with this key and version already exists.");
            }

            _items.RemoveAll(i => i.DefinitionId == definition.DefinitionId);
            _items.Add(definition);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRunRepository : IAgentWorkflowRunRepository
    {
        private readonly Dictionary<Guid, AgentWorkflowRun> _items = [];

        public Task<IReadOnlyList<AgentWorkflowRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowRun>>(_items.Values.Where(i => i.Scope == scope).ToArray());

        public Task<AgentWorkflowRun?> GetAsync(Guid runId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.TryGetValue(runId, out var run) && run.Scope == scope ? run : null);

        public Task SaveAsync(AgentWorkflowRun run, CancellationToken cancellationToken = default)
        {
            _items[run.RunId] = run;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryWorkItemRepository : IAgentWorkItemRepository
    {
        private readonly Dictionary<Guid, AgentWorkItem> _items = [];

        public Task<IReadOnlyList<AgentWorkItem>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkItem>>(_items.Values.Where(i => i.Scope == scope).ToArray());

        public Task<AgentWorkItem?> GetAsync(Guid workItemId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.TryGetValue(workItemId, out var item) && item.Scope == scope ? item : null);

        public Task<AgentWorkItem?> GetForNodeAsync(Guid runId, string nodeKey, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(i =>
                i.RunId == runId && i.NodeKey.Equals(nodeKey, StringComparison.OrdinalIgnoreCase) && i.Scope == scope));

        public Task SaveAsync(AgentWorkItem workItem, CancellationToken cancellationToken = default)
        {
            _items[workItem.WorkItemId] = workItem;
            return Task.CompletedTask;
        }
    }
}
