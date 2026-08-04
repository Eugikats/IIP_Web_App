using System.Text.Json;
using InsuranceIntelligence.Api.AgentWorkflows;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// RF-05: field correction and return-for-correction invalidate descendant workflow
/// state and re-queue evaluation so stale extraction outcomes cannot proceed.
/// </summary>
public sealed class InvalidateDownstreamAfterCorrectionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task ReturnForCorrection_Invalidates_All_Descendants_Not_Only_Correction_Target()
    {
        var runs = new InMemoryRunRepository();
        var workItems = new InMemoryWorkItemRepository();
        var audits = new RecordingAuditWriter();
        var service = CreateWorkflowService(runs, workItems, audits);

        var start = await AgentWorkflowEndpoints.StartRun(
            "document-to-decision",
            new StartAgentWorkflowRequest("case", "case-rf05-return", "corr-rf05-return"),
            service,
            CancellationToken.None);
        var (_, run) = await ExecuteAsync<AgentWorkflowRun>(start);

        await AdvanceThroughQualityGateAsync(service, run.RunId);
        var waiting = await service.GetRunAsync(run.RunId);
        Assert.Equal(AgentWorkflowRunStatus.WaitingForHuman, waiting!.Status);
        Assert.Contains(waiting.NodeRuns, n => n.NodeKey == "document-check" && n.Status == AgentWorkflowNodeRunStatus.Completed);
        Assert.Contains(waiting.NodeRuns, n => n.NodeKey == "quality-gate" && n.Status == AgentWorkflowNodeRunStatus.Completed);
        Assert.Contains(waiting.NodeRuns, n => n.NodeKey == "extraction-review" && n.Status == AgentWorkflowNodeRunStatus.WaitingForHuman);

        var workItem = Assert.Single(await service.ListWorkItemsAsync(), item => item.RunId == run.RunId);
        var decide = await AgentWorkflowEndpoints.DecideWorkItem(
            workItem.WorkItemId,
            new DecideAgentWorkItemRequest(AgentWorkItemDecision.ReturnForCorrection, "Extraction confidence was wrong after human check."),
            service,
            CancellationToken.None);
        var (decideStatus, _) = await ExecuteAsync<AgentWorkItem>(decide);
        Assert.Equal(StatusCodes.Status200OK, decideStatus);

        var reset = await service.GetRunAsync(run.RunId);
        Assert.NotNull(reset);
        Assert.Equal(AgentWorkflowNodeRunStatus.Queued, Assert.Single(reset.NodeRuns, n => n.NodeKey == "document-check").Status);
        Assert.Equal(AgentWorkflowNodeRunStatus.Pending, Assert.Single(reset.NodeRuns, n => n.NodeKey == "quality-gate").Status);
        Assert.Equal(AgentWorkflowNodeRunStatus.Pending, Assert.Single(reset.NodeRuns, n => n.NodeKey == "extraction-review").Status);
        Assert.Equal(AgentWorkflowNodeRunStatus.Pending, Assert.Single(reset.NodeRuns, n => n.NodeKey == "complete").Status);
        Assert.Null(Assert.Single(reset.NodeRuns, n => n.NodeKey == "quality-gate").OutputSummary);
        Assert.Contains(audits.Events, e => e.ActionName == "agent_workflow.nodes.invalidated");
        Assert.Contains(audits.Events, e => e.ActionName == "agent_workflow.nodes.reevaluated");
        var invalidation = Assert.Single(audits.Events, e => e.ActionName == "agent_workflow.nodes.invalidated");
        Assert.Contains("quality-gate:Completed", invalidation.BeforeStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extraction-review:WaitingForHuman", invalidation.BeforeStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorrectExtractionField_Invalidates_Case_Workflow_And_Requeues_Document_Check()
    {
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var runs = new InMemoryRunRepository();
        var workItems = new InMemoryWorkItemRepository();
        var workflowAudits = new RecordingAuditWriter();
        var workflowService = CreateWorkflowService(runs, workItems, workflowAudits);

        var claimCase = await new CaseIntakeService(caseRepository, auditRepository).CreateCaseAsync(
            new CreateCaseCommand(
                Scope,
                "Amina Okello",
                "amina@example.test",
                "+256700000000",
                "POL-RF05",
                "Motor",
                "Accident",
                "WebPortal",
                new DateOnly(2026, 5, 24),
                "Evidence correction replay",
                "user-claims-1",
                "corr-rf05-field"));

        var start = await AgentWorkflowEndpoints.StartRun(
            "document-to-decision",
            new StartAgentWorkflowRequest("case", claimCase.CaseId.ToString(), "corr-rf05-field"),
            workflowService,
            CancellationToken.None);
        var (_, run) = await ExecuteAsync<AgentWorkflowRun>(start);
        await AdvanceThroughQualityGateAsync(workflowService, run.RunId);

        var documentId = Guid.NewGuid();
        var document = new CaseDocument
        {
            DocumentId = documentId,
            CaseId = claimCase.CaseId,
            Scope = Scope,
            OriginalFileName = "claim-form.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 128,
            ObjectKey = $"{Scope.TenantId}/{Scope.WorkspaceId}/{claimCase.CaseId}/{documentId}_claim-form.pdf",
            DocumentType = DocumentType.ClaimForm,
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(DocumentType.ClaimForm, 0.99m, "Test", DateTimeOffset.UtcNow),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string> { ["sourceFileName"] = "claim-form.pdf" },
                new Dictionary<string, decimal> { ["sourceFileName"] = 0.9m },
                "Test",
                DateTimeOffset.UtcNow,
                false,
                null,
                null),
            ValidationWarnings = [],
            UploadedByUserId = "user-claims-1",
            UploadedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-rf05-field"
        };
        await documentRepository.SaveAsync(document);

        const string auditReason = "Handler verified the scanned filename against the paper form.";
        var result = await DocumentEndpoints.CorrectExtractionField(
            claimCase.CaseId,
            documentId,
            new CorrectExtractionFieldRequest("sourceFileName", "verified-claim-form.pdf"),
            caseRepository,
            documentRepository,
            auditRepository,
            workflowService,
            ScopedContext(auditReason),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<DocumentResponse>(result);
        Assert.Equal(StatusCodes.Status200OK, status);

        var reset = await workflowService.GetRunAsync(run.RunId);
        Assert.NotNull(reset);
        Assert.Equal(AgentWorkflowNodeRunStatus.Queued, Assert.Single(reset.NodeRuns, n => n.NodeKey == "document-check").Status);
        Assert.Equal(AgentWorkflowNodeRunStatus.Pending, Assert.Single(reset.NodeRuns, n => n.NodeKey == "quality-gate").Status);
        Assert.Equal(AgentWorkflowNodeRunStatus.Pending, Assert.Single(reset.NodeRuns, n => n.NodeKey == "extraction-review").Status);
        Assert.DoesNotContain(
            reset.NodeRuns,
            n => n.NodeKey == "quality-gate" && n.Status == AgentWorkflowNodeRunStatus.Completed);

        Assert.Contains(workflowAudits.Events, e => e.ActionName == "agent_workflow.nodes.invalidated");
        Assert.Contains(workflowAudits.Events, e => e.ActionName == "agent_workflow.nodes.reevaluated");

        var openAfterCorrection = (await workflowService.ListWorkItemsAsync())
            .Where(item =>
                item.RunId == run.RunId
                && item.NodeKey.Equals("extraction-review", StringComparison.OrdinalIgnoreCase)
                && item.Status is AgentWorkItemStatus.RequiresReview
                    or AgentWorkItemStatus.InProgress
                    or AgentWorkItemStatus.Escalated)
            .ToArray();
        Assert.Empty(openAfterCorrection);

        // Idempotent second correction must not resurrect stale completed quality/review outputs.
        var second = await DocumentEndpoints.CorrectExtractionField(
            claimCase.CaseId,
            documentId,
            new CorrectExtractionFieldRequest("sourceFileName", "verified-claim-form-v2.pdf"),
            caseRepository,
            documentRepository,
            auditRepository,
            workflowService,
            ScopedContext("Second correction after re-queue."),
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, (await ExecuteAsync<DocumentResponse>(second)).StatusCode);
        var again = await workflowService.GetRunAsync(run.RunId);
        Assert.Equal(AgentWorkflowNodeRunStatus.Pending, Assert.Single(again!.NodeRuns, n => n.NodeKey == "quality-gate").Status);
        Assert.Equal(2, workflowAudits.Events.Count(e => e.ActionName == "agent_workflow.nodes.invalidated"));
    }

    private static async Task AdvanceThroughQualityGateAsync(IAgentWorkflowService service, Guid runId)
    {
        var afterCheck = await AgentWorkflowEndpoints.CompleteNode(
            runId,
            "document-check",
            new CompleteAgentWorkflowNodeRequest(
                "ready",
                "Document check completed with structured fields only.",
                [new AgentEvidenceReference("document", "doc-1", "Claim form", null, null, 0.91m, null)],
                "AgentWorkflowEngine",
                "document-checker",
                "v1",
                null),
            service,
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, (await ExecuteAsync<AgentWorkflowRun>(afterCheck)).StatusCode);

        var afterGate = await AgentWorkflowEndpoints.CompleteNode(
            runId,
            "quality-gate",
            new CompleteAgentWorkflowNodeRequest(
                "review",
                "Evidence quality requires human extraction review.",
                [new AgentEvidenceReference("document", "doc-1", "Claim form", null, null, 0.88m, null)],
                "AgentWorkflowEngine",
                "workflow.evidence-quality",
                "v1",
                null),
            service,
            CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, (await ExecuteAsync<AgentWorkflowRun>(afterGate)).StatusCode);
    }

    private static AgentWorkflowService CreateWorkflowService(
        InMemoryRunRepository runs,
        InMemoryWorkItemRepository workItems,
        IAuditWriter audits) =>
        new(
            new FixedContext("user-claims-1"),
            new AllowPermissionService(),
            new InMemoryDefinitionRepository(),
            runs,
            workItems,
            new NoOpQueueDispatcher(),
            audits);

    private static DefaultHttpContext ScopedContext(string auditReason)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-rf05" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["X-Correlation-ID"] = "corr-rf05-field";
        context.Request.Headers["X-Audit-Reason"] = auditReason;
        return context;
    }

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

    private sealed class InMemoryDefinitionRepository : IAgentWorkflowDefinitionRepository
    {
        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowDefinition>>([]);

        public Task<IReadOnlyList<AgentWorkflowDefinition>> ListByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkflowDefinition>>([]);

        public Task<AgentWorkflowDefinition?> GetAsync(Guid definitionId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentWorkflowDefinition?>(null);

        public Task<AgentWorkflowDefinition?> GetByKeyAndVersionAsync(string key, int version, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentWorkflowDefinition?>(null);

        public Task<AgentWorkflowDefinition?> GetDraftByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentWorkflowDefinition?>(null);

        public Task<AgentWorkflowDefinition?> GetLatestPublishedByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentWorkflowDefinition?>(null);

        public Task<int> GetMaxVersionAsync(string key, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
            Task.FromResult(_items.Values
                .Where(i => i.RunId == runId && i.NodeKey.Equals(nodeKey, StringComparison.OrdinalIgnoreCase) && i.Scope == scope)
                .OrderByDescending(i => i.UpdatedAt)
                .FirstOrDefault());

        public Task SaveAsync(AgentWorkItem workItem, CancellationToken cancellationToken = default)
        {
            _items[workItem.WorkItemId] = workItem;
            return Task.CompletedTask;
        }
    }
}
