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
/// RF-04 seams: publish/save and start/execute reject R2+ / out-of-scope tools
/// and graphs; canonical Release 1 path remains executable.
/// </summary>
public sealed class BoundOrchestrationReleaseOneContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Theory]
    [InlineData("fnol-claim-readiness")]
    [InlineData("claims-triage-sla")]
    [InlineData("policy-customer-response")]
    [InlineData("underwriting-management-insight")]
    public async Task Start_Rejects_Held_Release_Two_Through_Five_Graphs(string workflowKey)
    {
        var result = await AgentWorkflowEndpoints.StartRun(
            workflowKey,
            new StartAgentWorkflowRequest("case", "case-rf04-held", null),
            CreateWorkflowService(),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        var message = body.GetProperty("errors").GetProperty("workflowKey")[0].GetString()!;
        Assert.Contains("held", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Release 1", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveDraft_Rejects_Out_Of_Scope_Release_Two_Agent()
    {
        var request = new SaveAgentWorkflowDraftRequest(
            null,
            "tenant-fnol-copy",
            "Tenant FNOL copy",
            "Must not publish R2 agents on Release 1 path.",
            ["case", "claim"],
            [
                Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
                Node("claim-brief", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ReadOnly, "claims-summary"),
                Node("review", AgentWorkflowNodeType.HumanReview, AgentWorkflowSideEffect.ApprovalRequired),
                Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
            ],
            [
                Edge("trigger", "claim-brief"),
                Edge("claim-brief", "review"),
                Edge("review", "complete")
            ]);

        var result = await AgentWorkflowEndpoints.SaveDraft(request, CreateWorkflowService(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        var message = body.GetProperty("errors").GetProperty("nodes")[0].GetString()!;
        Assert.Contains("claims-summary", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Release 1", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveDraft_Rejects_Out_Of_Scope_Tool()
    {
        var request = new SaveAgentWorkflowDraftRequest(
            null,
            "tenant-policy-tool",
            "Out of scope tool",
            "policies.read is not on the Release 1 allow-list.",
            ["case"],
            [
                Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
                Node("policy", AgentWorkflowNodeType.Tool, AgentWorkflowSideEffect.ReadOnly, toolKey: "policies.read"),
                Node("review", AgentWorkflowNodeType.HumanReview, AgentWorkflowSideEffect.ApprovalRequired),
                Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
            ],
            [
                Edge("trigger", "policy"),
                Edge("policy", "review"),
                Edge("review", "complete")
            ]);

        var result = await AgentWorkflowEndpoints.SaveDraft(request, CreateWorkflowService(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        var message = body.GetProperty("errors").GetProperty("nodes")[0].GetString()!;
        Assert.Contains("policies.read", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Release 1", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_And_Start_Succeed_For_Canonical_Release_One_Path()
    {
        var service = CreateWorkflowService();
        var releaseOne = AgentWorkflowCatalog.Find("document-to-decision")!;

        var save = await AgentWorkflowEndpoints.SaveDraft(
            new SaveAgentWorkflowDraftRequest(
                null,
                "tenant-document-to-decision-rf04",
                "Tenant Release 1 copy",
                releaseOne.Description,
                releaseOne.BusinessReferenceTypes,
                releaseOne.Nodes,
                releaseOne.Edges),
            service,
            CancellationToken.None);
        var (saveStatus, saved) = await ExecuteAsync<AgentWorkflowDefinition>(save);
        Assert.Equal(StatusCodes.Status201Created, saveStatus);

        var publish = await AgentWorkflowEndpoints.Publish(saved.Key, service, CancellationToken.None);
        var (publishStatus, published) = await ExecuteAsync<AgentWorkflowDefinition>(publish);
        Assert.Equal(StatusCodes.Status200OK, publishStatus);
        Assert.Equal(AgentWorkflowDefinitionStatus.Published, published.Status);

        var startSystem = await AgentWorkflowEndpoints.StartRun(
            "document-to-decision",
            new StartAgentWorkflowRequest("document", "doc-rf04-001", "corr-rf04-r1"),
            service,
            CancellationToken.None);
        var (startSystemStatus, systemRun) = await ExecuteAsync<AgentWorkflowRun>(startSystem);
        Assert.Equal(StatusCodes.Status202Accepted, startSystemStatus);
        Assert.Equal("document-to-decision", systemRun.WorkflowKey);

        var startTenant = await AgentWorkflowEndpoints.StartRun(
            saved.Key,
            new StartAgentWorkflowRequest("case", "case-rf04-001", "corr-rf04-tenant"),
            service,
            CancellationToken.None);
        var (startTenantStatus, tenantRun) = await ExecuteAsync<AgentWorkflowRun>(startTenant);
        Assert.Equal(StatusCodes.Status202Accepted, startTenantStatus);
        Assert.Equal(saved.Key, tenantRun.WorkflowKey);
    }

    [Fact]
    public async Task ListDefinitions_Exposes_Held_Availability_Language_For_Later_Releases()
    {
        var definitions = await CreateWorkflowService().ListDefinitionsAsync();
        var held = definitions.Where(d => d.IsSystemDefinition && d.Release is >= 2 and <= 5).ToArray();

        Assert.Equal(4, held.Length);
        Assert.All(held, flow =>
        {
            Assert.Equal("Held", AgentWorkflowReleaseOneBound.AvailabilityStatus(flow));
            Assert.Contains("held", flow.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Release 1", flow.Description, StringComparison.OrdinalIgnoreCase);
        });

        var releaseOne = Assert.Single(definitions, d => d.Key == "document-to-decision");
        Assert.Equal("Executable", AgentWorkflowReleaseOneBound.AvailabilityStatus(releaseOne));
    }

    private static AgentWorkflowService CreateWorkflowService() =>
        new(
            new FixedContext(),
            new AllowPermissionService(),
            new InMemoryDefinitionRepository(),
            new InMemoryRunRepository(),
            new InMemoryWorkItemRepository(),
            new NoOpQueueDispatcher(),
            new NullAuditWriter());

    private static AgentWorkflowNode Node(
        string key,
        AgentWorkflowNodeType type,
        AgentWorkflowSideEffect sideEffect,
        string? agentKey = null,
        string? toolKey = null) =>
        new(
            key,
            key,
            key,
            type,
            agentKey,
            toolKey,
            sideEffect,
            new AgentWorkflowPosition(0, 0),
            RequiresHumanReview: type == AgentWorkflowNodeType.HumanReview || sideEffect == AgentWorkflowSideEffect.ApprovalRequired,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static AgentWorkflowEdge Edge(string source, string target) => new(source, target, null, null);

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

    private sealed class FixedContext : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-claims", "user-claims-1", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class AllowPermissionService : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NullAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpQueueDispatcher : IMessageQueueDispatcher
    {
        public Task DispatchAsync(QueueMessageEnvelope envelope, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryDefinitionRepository : IAgentWorkflowDefinitionRepository
    {
        private readonly List<AgentWorkflowDefinition> _items = [];

        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowDefinition>>(_items.Where(i => i.Scope == scope).ToArray());

        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
        {
            var normalized = key.Trim().ToLowerInvariant();
            return Task.FromResult<IReadOnlyList<AgentWorkflowDefinition>>(
                _items.Where(i => i.Scope == scope && string.Equals(i.Key, normalized, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(i => i.Version)
                    .ToArray());
        }

        public Task<AgentWorkflowDefinition?> GetAsync(Guid definitionId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.DefinitionId == definitionId && i.Scope == scope));

        public Task<AgentWorkflowDefinition?> GetByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.Scope == scope && string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)));

        public Task<AgentWorkflowDefinition?> GetByKeyAndVersionAsync(string key, int version, TenantScope scope, CancellationToken cancellationToken = default)
        {
            var normalized = key.Trim().ToLowerInvariant();
            return Task.FromResult(_items.FirstOrDefault(i =>
                i.Scope == scope
                && string.Equals(i.Key, normalized, StringComparison.OrdinalIgnoreCase)
                && i.Version == version));
        }

        public Task<AgentWorkflowDefinition?> GetDraftByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
        {
            var normalized = key.Trim().ToLowerInvariant();
            return Task.FromResult(_items.FirstOrDefault(i =>
                i.Scope == scope
                && string.Equals(i.Key, normalized, StringComparison.OrdinalIgnoreCase)
                && i.Status == AgentWorkflowDefinitionStatus.Draft));
        }

        public Task<AgentWorkflowDefinition?> GetLatestPublishedByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
        {
            var normalized = key.Trim().ToLowerInvariant();
            return Task.FromResult(_items
                .Where(i => i.Scope == scope
                    && string.Equals(i.Key, normalized, StringComparison.OrdinalIgnoreCase)
                    && i.Status == AgentWorkflowDefinitionStatus.Published)
                .OrderByDescending(i => i.Version)
                .FirstOrDefault());
        }

        public Task<int> GetMaxVersionAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
        {
            var normalized = key.Trim().ToLowerInvariant();
            var max = _items
                .Where(i => i.Scope == scope && string.Equals(i.Key, normalized, StringComparison.OrdinalIgnoreCase))
                .Select(i => i.Version)
                .DefaultIfEmpty(0)
                .Max();
            return Task.FromResult(max);
        }

        public Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(i => i.DefinitionId == definition.DefinitionId);
            _items.Add(definition);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRunRepository : IAgentWorkflowRunRepository
    {
        private readonly Dictionary<Guid, AgentWorkflowRun> _runs = [];

        public Task<IReadOnlyList<AgentWorkflowRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowRun>>(_runs.Values.Where(r => r.Scope == scope).ToArray());

        public Task<AgentWorkflowRun?> GetAsync(Guid runId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_runs.TryGetValue(runId, out var run) && run.Scope == scope ? run : null);

        public Task SaveAsync(AgentWorkflowRun run, CancellationToken cancellationToken = default)
        {
            _runs[run.RunId] = run;
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
