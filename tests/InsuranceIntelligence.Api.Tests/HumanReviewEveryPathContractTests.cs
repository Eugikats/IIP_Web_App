using System.Text.Json;
using InsuranceIntelligence.Api.AgentWorkflows;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// RF-02 seams: publish all-paths human-review invariant + ApprovalDecisionGuard on Decide/Send/Execute.
/// </summary>
public sealed class HumanReviewEveryPathContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task SaveDraft_Rejects_Bypass_Branch_Cycle_And_Unreachable_Graphs()
    {
        await AssertRejected(BypassRequest("unsafe-bypass"), "human review");
        await AssertRejected(BranchBypassRequest("unsafe-branch"), "human review");
        await AssertRejected(CycleRequest("unsafe-cycle"), "cycle");
        await AssertRejected(UnreachableRequest("unsafe-orphan"), "unreachable");
    }

    [Fact]
    public async Task Publish_Succeeds_For_Canonical_Release_One_Shape()
    {
        var service = CreateWorkflowService();
        var releaseOne = AgentWorkflowCatalog.Find("document-to-decision")!;
        var save = await AgentWorkflowEndpoints.SaveDraft(
            new SaveAgentWorkflowDraftRequest(
                null,
                "tenant-document-to-decision",
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
    }

    [Theory]
    [InlineData("agent:document-checker")]
    [InlineData("system:orchestrator")]
    [InlineData("integration:webhook")]
    public async Task ApproveApproval_Blocks_NonHuman_Actors(string actorUserId)
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        var approval = await CreatePendingApprovalAsync(deps, claimCase);

        var result = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Agent attempted consequential approve."),
            deps.ApprovalRepository,
            deps.AuditRepository,
            ActorContext(actorUserId, "Agent must not approve."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("human_decision_required", body.GetProperty("code").GetString());
        var persisted = await deps.ApprovalRepository.GetAsync(
            approval.ApprovalId,
            TenantScope.Create("tenant-demo", "workspace-claims"),
            CancellationToken.None);
        Assert.Equal(ApprovalStatus.Pending, persisted!.Status);
    }

    [Fact]
    public async Task ExecuteApprovedNextStep_Blocks_Agent_Actor()
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        var approval = await CreatePendingApprovalAsync(deps, claimCase);

        var approve = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Human approved."),
            deps.ApprovalRepository,
            deps.AuditRepository,
            ActorContext("user-claims-1", "Human approve."),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, (await ExecuteAsync<ApprovalResponse>(approve)).StatusCode);

        var execute = await ApprovalEndpoints.ExecuteApprovedNextStep(
            approval.ApprovalId,
            new ExecuteApprovedNextStepRequest("Agent execute."),
            deps.ApprovalRepository,
            deps.CaseRepository,
            deps.AuditRepository,
            ActorContext("agent:runtime", "Agent execute."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(execute);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("human_decision_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SendCustomerUpdate_Blocks_System_Actor()
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        var draft = await CreateApprovedCustomerUpdateAsync(deps, claimCase);

        var send = await CustomerUpdateEndpoints.SendCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest("customer@example.test"),
            deps.CaseRepository,
            deps.CustomerUpdateRepository,
            deps.CommunicationProvider,
            deps.AuditRepository,
            ActorContext("system:batch-sender", "System send."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(send);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("human_decision_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DecideWorkItem_Blocks_Agent_Actor()
    {
        var workItems = new InMemoryWorkItemRepository();
        var item = new AgentWorkItem(
            Guid.NewGuid(),
            TenantScope.Create("tenant-demo", "workspace-claims"),
            Guid.NewGuid(),
            "document-to-decision",
            "extraction-review",
            "Review",
            "Confirm extraction.",
            "case",
            "case-1",
            AgentWorkItemStatus.RequiresReview,
            [],
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            "corr-1");
        await workItems.SaveAsync(item);

        var service = CreateWorkflowService(userId: "agent:runtime", workItems: workItems);
        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            service.DecideWorkItemAsync(
                item.WorkItemId,
                new DecideAgentWorkItemRequest(AgentWorkItemDecision.AcceptPreparedWork, "Automated accept")));

        Assert.Contains("human", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveActorType_Maps_Prefixed_Identities()
    {
        Assert.Equal(DecisionActorType.Agent, ApprovalDecisionGuard.ResolveActorType("agent:x"));
        Assert.Equal(DecisionActorType.System, ApprovalDecisionGuard.ResolveActorType("system:x"));
        Assert.Equal(DecisionActorType.Integration, ApprovalDecisionGuard.ResolveActorType("integration:x"));
        Assert.Equal(DecisionActorType.Human, ApprovalDecisionGuard.ResolveActorType("entra:tid:oid"));
        Assert.Equal(DecisionActorType.System, ApprovalDecisionGuard.ResolveActorType(null));
    }

    private static async Task AssertRejected(SaveAgentWorkflowDraftRequest request, string fragment)
    {
        var result = await AgentWorkflowEndpoints.SaveDraft(request, CreateWorkflowService(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains(fragment, body.GetProperty("errors").GetProperty("nodes")[0].GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static SaveAgentWorkflowDraftRequest BypassRequest(string key) =>
        new(null, key, "Bypass", "No review.", ["case"],
            [
                Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
                Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, "document-checker"),
                Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
            ],
            [Edge("trigger", "prepare"), Edge("prepare", "complete")]);

    private static SaveAgentWorkflowDraftRequest BranchBypassRequest(string key) =>
        new(null, key, "Branch bypass", "One safe path, one bypass.", ["case"],
            [
                Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
                Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, "document-checker"),
                Node("review", AgentWorkflowNodeType.HumanReview, AgentWorkflowSideEffect.ApprovalRequired),
                Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
            ],
            [
                Edge("trigger", "prepare"),
                Edge("prepare", "review"),
                Edge("review", "complete"),
                Edge("prepare", "complete")
            ]);

    private static SaveAgentWorkflowDraftRequest CycleRequest(string key) =>
        new(null, key, "Cycle", "Cycle without review.", ["case"],
            [
                Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
                Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, "document-checker"),
                Node("loop-a", AgentWorkflowNodeType.Tool, AgentWorkflowSideEffect.ReadOnly, toolKey: "documents.read"),
                Node("loop-b", AgentWorkflowNodeType.Decision, AgentWorkflowSideEffect.ReadOnly, toolKey: "workflow.evidence-quality"),
                Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
            ],
            [
                Edge("trigger", "prepare"),
                Edge("prepare", "loop-a"),
                Edge("loop-a", "loop-b"),
                Edge("loop-b", "prepare"),
                Edge("loop-b", "complete")
            ]);

    private static SaveAgentWorkflowDraftRequest UnreachableRequest(string key) =>
        new(null, key, "Unreachable", "Orphan consequential.", ["case"],
            [
                Node("trigger", AgentWorkflowNodeType.Trigger, AgentWorkflowSideEffect.ReadOnly),
                Node("prepare", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, "document-checker"),
                Node("orphan", AgentWorkflowNodeType.Agent, AgentWorkflowSideEffect.ApprovalRequired, "document-checker"),
                Node("review", AgentWorkflowNodeType.HumanReview, AgentWorkflowSideEffect.ApprovalRequired),
                Node("complete", AgentWorkflowNodeType.Complete, AgentWorkflowSideEffect.ReadOnly)
            ],
            [
                Edge("trigger", "prepare"),
                Edge("prepare", "review"),
                Edge("review", "complete")
            ]);

    private static AgentWorkflowService CreateWorkflowService(
        string userId = "user-claims-1",
        IAgentWorkItemRepository? workItems = null) =>
        new(
            new FixedContext(userId),
            new AllowPermissionService(),
            new InMemoryDefinitionRepository(),
            new InMemoryRunRepository(),
            workItems ?? new InMemoryWorkItemRepository(),
            new NoOpQueueDispatcher(),
            new NullAuditWriter());

    private static WorkflowDeps CreateWorkflowDeps() =>
        new(
            new InMemoryCaseRepository(),
            new InMemoryAuditRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryCustomerUpdateRepository(),
            new MockCommunicationProvider());

    private static async Task<ClaimCase> CreateCaseAsync(WorkflowDeps deps) =>
        await new CaseIntakeService(deps.CaseRepository, deps.AuditRepository).CreateCaseAsync(
            new CreateCaseCommand(
                TenantScope.Create("tenant-demo", "workspace-claims"),
                "Amina Okello",
                "amina@example.test",
                "+256700000000",
                "POL-1001",
                "Motor",
                "Accident",
                "WebPortal",
                new DateOnly(2026, 5, 24),
                "Minor collision reported at branch intake.",
                "user-claims-1",
                "corr-rf02-001"));

    private static async Task<ApprovalRequest> CreatePendingApprovalAsync(WorkflowDeps deps, ClaimCase claimCase)
    {
        var approval = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            ApprovalType = "SettlementStep",
            Status = ApprovalStatus.Pending,
            RiskLevel = "Medium",
            EvidenceSummary = "Prepared settlement recommendation.",
            SourceReferences = ["case:" + claimCase.CaseId],
            RequestedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-rf02-approval"
        };
        await deps.ApprovalRepository.SaveAsync(approval, CancellationToken.None);
        return approval;
    }

    private static async Task<CustomerUpdateDraft> CreateApprovedCustomerUpdateAsync(WorkflowDeps deps, ClaimCase claimCase)
    {
        var draft = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            Channel = CommunicationChannel.Email,
            Status = CustomerUpdateStatus.Approved,
            ReasonCategory = CustomerUpdateReasonCategory.StatusUpdate,
            MessageBody = "Your claim is under review.",
            RequiresApproval = true,
            Sensitive = false,
            CreatedByUserId = "user-claims-1",
            ApprovedByUserId = "user-claims-1",
            ApprovedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-rf02-update"
        };
        await deps.CustomerUpdateRepository.SaveAsync(draft, CancellationToken.None);
        return draft;
    }

    private static DefaultHttpContext ActorContext(string userId, string auditReason)
    {
        var tenantContext = new TenantContextAccessor();
        // ClaimsSupervisor has ViewAllClaims so case visibility is not confused with the
        // human-decision ceiling under test (non-human actors must be blocked by the guard,
        // not by assignment filters returning 404).
        tenantContext.SetCurrent(new TenantContext("tenant-demo", "workspace-claims", userId, ["ClaimsSupervisor"]));
        var context = new DefaultHttpContext { TraceIdentifier = "trace-rf02" };
        context.Request.Headers["X-Audit-Reason"] = auditReason;
        context.RequestServices = new ServiceCollection()
            .AddSingleton<ITenantContextAccessor>(tenantContext)
            .BuildServiceProvider();
        return context;
    }

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

    private sealed record WorkflowDeps(
        InMemoryCaseRepository CaseRepository,
        InMemoryAuditRepository AuditRepository,
        InMemoryApprovalRepository ApprovalRepository,
        InMemoryCustomerUpdateRepository CustomerUpdateRepository,
        ICommunicationProvider CommunicationProvider);

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
            _items.RemoveAll(i => i.DefinitionId == definition.DefinitionId);
            _items.Add(definition);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRunRepository : IAgentWorkflowRunRepository
    {
        public Task<IReadOnlyList<AgentWorkflowRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowRun>>([]);
        public Task<AgentWorkflowRun?> GetAsync(Guid runId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentWorkflowRun?>(null);
        public Task SaveAsync(AgentWorkflowRun run, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
