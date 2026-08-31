using System.Text.Json;
using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Functions;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class AgentWorkflowExecutionWorkerContractTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public void Function_Uses_Configured_AgentWorkflow_Queue_Contract()
    {
        Assert.Equal("agent-workflow", AgentWorkflowExecutionFunction.AgentWorkflowQueueSetting);
        Assert.Equal("ServiceBusConnection", AgentWorkflowExecutionFunction.ServiceBusConnectionSetting);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_Malformed_Message_For_Retry_DeadLetter()
    {
        var worker = CreateWorker();
        await Assert.ThrowsAsync<InvalidDataException>(() => worker.ExecuteAsync("{}"));
    }

    [Fact]
    public async Task ExecuteAsync_Stops_Safely_At_HumanReview_Node()
    {
        var service = new RecordingWorkflowService(BuildRun(nodeType: AgentWorkflowNodeType.HumanReview));
        var worker = CreateWorker(workflowService: service);

        await worker.ExecuteAsync(BuildMessage(nodeType: "HumanReview"));

        Assert.Equal(0, service.CompleteCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Ignores_Duplicate_Delivery()
    {
        var run = BuildRun(status: AgentWorkflowNodeRunStatus.Completed, attemptCount: 1);
        var service = new RecordingWorkflowService(run);
        var worker = CreateWorker(workflowService: service);

        await worker.ExecuteAsync(BuildMessage(attempt: 1));

        Assert.Equal(0, service.CompleteCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_Stale_Version_Mismatch()
    {
        var run = BuildRun(workflowVersion: 1);
        var service = new RecordingWorkflowService(run);
        var worker = CreateWorker(workflowService: service);

        await Assert.ThrowsAsync<InvalidDataException>(() => worker.ExecuteAsync(BuildMessage(workflowVersion: 2)));
    }

    [Fact]
    public async Task ExecuteAsync_ProviderUnavailable_Throws_For_Retry()
    {
        var service = new RecordingWorkflowService(BuildRun());
        var worker = CreateWorker(
            workflowService: service,
            executor: new ThrowingExecutor(new InvalidOperationException("provider unavailable")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.ExecuteAsync(BuildMessage()));
        Assert.Equal(0, service.CompleteCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Completes_Bounded_Node_And_Writes_Safe_Result()
    {
        var service = new RecordingWorkflowService(BuildRun());
        var completion = new CompleteAgentWorkflowNodeRequest(
            Outcome: "ready",
            OutputSummary: "Bounded metadata execution complete.",
            Evidence: [new AgentEvidenceReference("document", "doc-1", "claim.pdf", null, null, 0.92m, null)],
            Provider: "IIPFunctionsWorker",
            ModelOrTool: "document-checker",
            PromptVersion: "rf06-v1",
            ErrorCode: null);
        var worker = CreateWorker(workflowService: service, executor: new FixedExecutor(completion));

        await worker.ExecuteAsync(BuildMessage());

        Assert.Equal(1, service.CompleteCalls);
        Assert.Equal("ready", service.LastCompletion!.Outcome);
        Assert.Equal("Bounded metadata execution complete.", service.LastCompletion.OutputSummary);
        Assert.DoesNotContain("ocr", service.LastCompletion.OutputSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", service.LastCompletion.OutputSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_Membership_Scope_Mismatch_With_Envelope()
    {
        var worker = new AgentWorkflowExecutionWorker(
            new RecordingContextSetter(),
            new FixedScopePermissionReadRepository("other-tenant", "other-workspace"),
            new RecordingWorkflowService(BuildRun()),
            new FixedExecutor(new CompleteAgentWorkflowNodeRequest(
                Outcome: "ready",
                OutputSummary: "ok",
                Evidence: [],
                Provider: "IIPFunctionsWorker",
                ModelOrTool: "document-checker",
                PromptVersion: "rf06-v1",
                ErrorCode: null)),
            NullLogger<AgentWorkflowExecutionWorker>.Instance,
            new InfrastructureOptions
            {
                ServiceBus = new ServiceBusOptions { AgentWorkflowWorkerUserId = "system:agent-workflow-worker" }
            });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => worker.ExecuteAsync(BuildMessage()));
        Assert.Contains("does not match the queue envelope", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentWorkflowExecutionWorker CreateWorker(
        RecordingWorkflowService? workflowService = null,
        IAgentWorkflowNodeExecutor? executor = null) =>
        new(
            new RecordingContextSetter(),
            new FixedPermissionReadRepository(),
            workflowService ?? new RecordingWorkflowService(BuildRun()),
            executor ?? new FixedExecutor(new CompleteAgentWorkflowNodeRequest(
                Outcome: "ready",
                OutputSummary: "ok",
                Evidence: [],
                Provider: "IIPFunctionsWorker",
                ModelOrTool: "document-checker",
                PromptVersion: "rf06-v1",
                ErrorCode: null)),
            NullLogger<AgentWorkflowExecutionWorker>.Instance,
            new InfrastructureOptions
            {
                ServiceBus = new ServiceBusOptions { AgentWorkflowWorkerUserId = "system:agent-workflow-worker" }
            });

    private static string BuildMessage(
        string nodeType = "Agent",
        int workflowVersion = 1,
        int attempt = 1)
    {
        var dispatch = new AgentWorkflowNodeDispatch(
            RunId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DefinitionId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            WorkflowKey: "document-to-decision",
            WorkflowVersion: workflowVersion,
            NodeKey: "document-check",
            NodeType: nodeType,
            AgentKey: nodeType == "Agent" ? "document-checker" : null,
            ToolKey: nodeType == "Tool" ? "workflow.evidence-quality" : null,
            BusinessReferenceType: "case",
            BusinessReferenceId: Guid.Parse("33333333-3333-3333-3333-333333333333").ToString(),
            Attempt: attempt);

        return JsonSerializer.Serialize(new
        {
            QueueName = "agent-workflow",
            MessageType = AgentWorkflowExecutionWorker.AgentWorkflowMessageType,
            TenantId = Scope.TenantId,
            WorkspaceId = Scope.WorkspaceId,
            CorrelationId = "corr-rf06",
            Payload = dispatch
        });
    }

    private static AgentWorkflowRun BuildRun(
        AgentWorkflowNodeRunStatus status = AgentWorkflowNodeRunStatus.Queued,
        AgentWorkflowNodeType nodeType = AgentWorkflowNodeType.Agent,
        int workflowVersion = 1,
        int attemptCount = 1) =>
        new(
            RunId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Scope: Scope,
            DefinitionId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            WorkflowKey: "document-to-decision",
            WorkflowVersion: workflowVersion,
            BusinessReferenceType: "case",
            BusinessReferenceId: Guid.Parse("33333333-3333-3333-3333-333333333333").ToString(),
            Status: AgentWorkflowRunStatus.Running,
            NodeRuns:
            [
                new AgentWorkflowNodeRun(
                    NodeKey: "document-check",
                    Status: status,
                    StartedAt: DateTimeOffset.UtcNow,
                    CompletedAt: null,
                    Outcome: null,
                    OutputSummary: null,
                    Evidence: [],
                    Provider: null,
                    ModelOrTool: nodeType == AgentWorkflowNodeType.Agent ? "document-checker" : "workflow.evidence-quality",
                    PromptVersion: null,
                    ErrorCode: null,
                    AttemptCount: attemptCount)
            ],
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CompletedAt: null,
            StartedByUserId: "user-claims-1",
            CorrelationId: "corr-rf06",
            FailureCode: null);

    private sealed class RecordingContextSetter : ITenantContextSetter
    {
        public TenantContext? Current { get; private set; }

        public void SetCurrent(TenantContext context)
        {
            Current = context;
        }
    }

    private sealed class FixedPermissionReadRepository : IPermissionReadRepository
    {
        public Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(string tenantId, string workspaceId, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Permission>>([Permission.ManageAgentWorkflows]);

        public Task<EffectiveMembership?> GetEffectiveMembershipAsync(string userId, string? tenantHint, string? workspaceHint, CancellationToken cancellationToken = default) =>
            Task.FromResult<EffectiveMembership?>(new EffectiveMembership(
                tenantHint ?? Scope.TenantId,
                workspaceHint ?? Scope.WorkspaceId,
                userId,
                ["Admin"],
                [Permission.ManageAgentWorkflows]));
    }

    private sealed class FixedScopePermissionReadRepository(string tenantId, string workspaceId) : IPermissionReadRepository
    {
        public Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(string tenantIdArg, string workspaceIdArg, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Permission>>([Permission.ManageAgentWorkflows]);

        public Task<EffectiveMembership?> GetEffectiveMembershipAsync(string userId, string? tenantHint, string? workspaceHint, CancellationToken cancellationToken = default) =>
            Task.FromResult<EffectiveMembership?>(new EffectiveMembership(
                tenantId,
                workspaceId,
                userId,
                ["Admin"],
                [Permission.ManageAgentWorkflows]));
    }

    private sealed class RecordingWorkflowService(AgentWorkflowRun run) : IAgentWorkflowService
    {
        public int CompleteCalls { get; private set; }
        public CompleteAgentWorkflowNodeRequest? LastCompletion { get; private set; }

        public Task<AgentWorkflowRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentWorkflowRun?>(run);

        public Task<AgentWorkflowRun> CompleteNodeAsync(Guid runId, string nodeKey, CompleteAgentWorkflowNodeRequest request, CancellationToken cancellationToken = default)
        {
            CompleteCalls++;
            LastCompletion = request;
            return Task.FromResult(run);
        }

        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListVersionsAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkflowDefinition?> GetDefinitionAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkflowDefinition?> GetVersionAsync(string key, int version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkflowDefinition> SaveDraftAsync(SaveAgentWorkflowDraftRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkflowDefinition> DraftNewVersionAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkflowDefinition> PublishAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AgentWorkflowRun>> ListRunsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkflowRun> StartAsync(string workflowKey, StartAgentWorkflowRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AgentWorkItem>> ListWorkItemsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkItem?> GetWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentWorkItem> DecideWorkItemAsync(Guid workItemId, DecideAgentWorkItemRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AgentWorkflowRun>> InvalidateDownstreamAfterEvidenceCorrectionAsync(string businessReferenceType, string businessReferenceId, string? auditReason = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedExecutor(CompleteAgentWorkflowNodeRequest completion) : IAgentWorkflowNodeExecutor
    {
        public Task<CompleteAgentWorkflowNodeRequest> ExecuteAsync(AgentWorkflowExecutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(completion);
    }

    private sealed class ThrowingExecutor(Exception error) : IAgentWorkflowNodeExecutor
    {
        public Task<CompleteAgentWorkflowNodeRequest> ExecuteAsync(AgentWorkflowExecutionContext context, CancellationToken cancellationToken = default) =>
            Task.FromException<CompleteAgentWorkflowNodeRequest>(error);
    }
}
