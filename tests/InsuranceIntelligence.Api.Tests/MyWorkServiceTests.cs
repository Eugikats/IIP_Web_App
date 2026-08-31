using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.MyWork;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class MyWorkServiceTests
{
    [Fact]
    public async Task ListAsync_AggregatesPermittedSourceWorkWithoutMergingSourceState()
    {
        var scope = TenantScope.Create("tenant-work", "workspace-claims");
        var userId = "claims-user-1";
        var context = Context(scope, userId);
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        var tasks = new InMemoryWorkflowTaskRepository();
        var approvals = new InMemoryApprovalRepository();
        var updates = new InMemoryCustomerUpdateRepository();
        var agentItems = new StubAgentWorkItemRepository();
        var claimCase = Case(scope, userId, DateTimeOffset.UtcNow.AddHours(-1));
        await cases.SaveAsync(claimCase);
        await documents.SaveAsync(Document(scope, claimCase.CaseId));
        await tasks.SaveAsync(TaskItem(scope, claimCase.CaseId, userId));
        await approvals.SaveAsync(Approval(scope, claimCase.CaseId, userId));
        await updates.SaveAsync(CustomerUpdate(scope, claimCase.CaseId, userId));
        await agentItems.SaveAsync(AgentItem(scope, claimCase.CaseId.ToString(), userId));

        var audit = new StubAuditWriter();
        var service = Service(
            cases, documents, tasks, approvals, updates, agentItems, context,
            new StubPermissionService(
                Permission.ViewAssignedClaims,
                Permission.ApproveSettlementWorkflowStep,
                Permission.ManageCustomerCommunications,
                Permission.ReviewAgentWorkItems),
            audit);

        var response = await service.ListAsync(new MyWorkQuery(MyWorkOwnership.Mine, null, null, 50), CancellationToken.None);

        Assert.Equal(5, response.Items.Count);
        Assert.Equal(
            [MyWorkSourceType.WorkflowTask, MyWorkSourceType.Approval, MyWorkSourceType.DocumentReview, MyWorkSourceType.CommunicationReview, MyWorkSourceType.AgentWorkItem],
            response.Items.Select(item => item.SourceType).Order().ToArray());
        Assert.All(response.Items, item => Assert.Equal(userId, item.OwnerUserId));
        Assert.Contains(response.Items, item =>
            item.SourceType == MyWorkSourceType.Approval
            && item.SourceStatus == ApprovalStatus.Pending.ToString()
            && item.ApprovalRequired);
        Assert.Contains(response.Items, item =>
            item.SourceType == MyWorkSourceType.DocumentReview
            && item.EvidenceReadiness == MyWorkEvidenceReadiness.Gaps);
        Assert.Equal("my-work.viewed", audit.LastEvent?.ActionName);
        Assert.DoesNotContain("Sensitive", audit.LastEvent?.AuditReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListAsync_MineOmitsOtherUsersUnassignedAndOtherTenantWork()
    {
        var scope = TenantScope.Create("tenant-work", "workspace-claims");
        var otherScope = TenantScope.Create("tenant-other", "workspace-claims");
        var userId = "claims-user-1";
        var context = Context(scope, userId);
        var cases = new InMemoryCaseRepository();
        var tasks = new InMemoryWorkflowTaskRepository();
        var mine = Case(scope, userId, DateTimeOffset.UtcNow.AddHours(2));
        var other = Case(scope, "claims-user-2", DateTimeOffset.UtcNow.AddHours(2));
        var outside = Case(otherScope, userId, DateTimeOffset.UtcNow.AddHours(2));
        await cases.SaveAsync(mine);
        await cases.SaveAsync(other);
        await cases.SaveAsync(outside);
        await tasks.SaveAsync(TaskItem(scope, mine.CaseId, userId));
        await tasks.SaveAsync(TaskItem(scope, other.CaseId, "claims-user-2"));
        await tasks.SaveAsync(TaskItem(scope, mine.CaseId, null));
        await tasks.SaveAsync(TaskItem(otherScope, outside.CaseId, userId));

        var service = Service(
            cases,
            new InMemoryCaseDocumentRepository(),
            tasks,
            new InMemoryApprovalRepository(),
            new InMemoryCustomerUpdateRepository(),
            new StubAgentWorkItemRepository(),
            context,
            new StubPermissionService(Permission.ViewAssignedClaims),
            new StubAuditWriter());

        var response = await service.ListAsync(new MyWorkQuery(MyWorkOwnership.Mine, null, null, 50), CancellationToken.None);

        var item = Assert.Single(response.Items);
        Assert.Equal(userId, item.OwnerUserId);
        Assert.Equal(mine.CaseId.ToString(), item.CaseId);
    }

    [Fact]
    public async Task ListAsync_AllOwnershipRequiresViewAllClaims()
    {
        var scope = TenantScope.Create("tenant-work", "workspace-claims");
        var service = Service(
            new InMemoryCaseRepository(),
            new InMemoryCaseDocumentRepository(),
            new InMemoryWorkflowTaskRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryCustomerUpdateRepository(),
            new StubAgentWorkItemRepository(),
            Context(scope, "claims-user-1"),
            new StubPermissionService(Permission.ViewAssignedClaims),
            new StubAuditWriter());

        var exception = await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            service.ListAsync(new MyWorkQuery(MyWorkOwnership.All, null, null, 50), CancellationToken.None));

        Assert.Equal(Permission.ViewAllClaims, exception.Permission);
    }

    private static MyWorkService Service(
        ICaseRepository cases,
        ICaseDocumentRepository documents,
        IWorkflowTaskRepository tasks,
        IApprovalRepository approvals,
        ICustomerUpdateRepository updates,
        IAgentWorkItemRepository agentItems,
        ITenantContextAccessor context,
        IPermissionService permissions,
        IAuditWriter audit) =>
        new(cases, documents, tasks, approvals, updates, agentItems, context, permissions, audit);

    private static TenantContextAccessor Context(TenantScope scope, string userId)
    {
        var context = new TenantContextAccessor();
        context.SetCurrent(new TenantContext(scope.TenantId, scope.WorkspaceId, userId, [PlatformRole.ClaimsOfficer.ToString()]));
        return context;
    }

    private static ClaimCase Case(TenantScope scope, string? assignedUserId, DateTimeOffset slaDueAt) =>
        new()
        {
            CaseId = Guid.NewGuid(), Scope = scope, CustomerName = "Test customer", PolicyNumber = "POL-001",
            ProductLine = "Motor", ClaimType = "Own damage", Channel = "Portal", IncidentDate = new DateOnly(2026, 8, 1),
            IncidentDescription = "Test incident", Status = CaseStatus.InReview, CompletenessScore = 65, RiskLevel = "Medium",
            Priority = CasePriority.High, SlaDueAt = slaDueAt, AssignedUserId = assignedUserId, AssignedTeam = "Motor Claims",
            CreatedByUserId = assignedUserId ?? "creator", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static WorkflowTask TaskItem(TenantScope scope, Guid caseId, string? owner) =>
        new()
        {
            TaskId = Guid.NewGuid(), Scope = scope, CaseId = caseId, TaskType = WorkflowTaskType.ReviewCase,
            Status = WorkflowTaskStatus.Open, Title = "Review claim", Summary = "Sensitive task summary",
            AssignedUserId = owner, AssignedTeam = "Motor Claims", CreatedByUserId = "creator",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3), UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static ApprovalRequest Approval(TenantScope scope, Guid caseId, string reviewer) =>
        new()
        {
            ApprovalId = Guid.NewGuid(), Scope = scope, CaseId = caseId, ApprovalType = "Settlement review",
            Status = ApprovalStatus.Pending, RiskLevel = "High", EvidenceSummary = "Sensitive approval evidence",
            SourceReferences = [], RequestedByUserId = "requester", ReviewerUserId = reviewer,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3), UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static CaseDocument Document(TenantScope scope, Guid caseId) =>
        new()
        {
            DocumentId = Guid.NewGuid(), CaseId = caseId, Scope = scope, OriginalFileName = "estimate.pdf",
            ContentType = "application/pdf", FileSizeBytes = 100, ObjectKey = "test/estimate.pdf",
            DocumentType = DocumentType.GarageEstimate, ProcessingStatus = DocumentProcessingStatus.ReviewRequired,
            Classification = new DocumentClassificationResult(DocumentType.GarageEstimate, 0.7m, "test", DateTimeOffset.UtcNow),
            Extraction = new DocumentExtractionResult(new Dictionary<string, string>(), new Dictionary<string, decimal>(), "test", DateTimeOffset.UtcNow, false, null, null),
            ValidationWarnings = [new DocumentValidationWarning("missing", "Sensitive warning", "High")],
            UploadedByUserId = "uploader", UploadedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ProcessedAt = DateTimeOffset.UtcNow.AddHours(-1), CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static CustomerUpdateDraft CustomerUpdate(TenantScope scope, Guid caseId, string creator) =>
        new()
        {
            UpdateId = Guid.NewGuid(), Scope = scope, CaseId = caseId, ReasonCategory = CustomerUpdateReasonCategory.StatusUpdate,
            Channel = CommunicationChannel.WhatsApp, Status = CustomerUpdateStatus.PendingApproval,
            MessageBody = "Sensitive customer message", RequiresApproval = true, Sensitive = true,
            CreatedByUserId = creator, CreatedAt = DateTimeOffset.UtcNow.AddHours(-2), UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static AgentWorkItem AgentItem(TenantScope scope, string caseId, string owner) =>
        new(Guid.NewGuid(), scope, Guid.NewGuid(), "claim-review", "human-review", "Review prepared claim brief",
            "Sensitive instructions", "Case", caseId, AgentWorkItemStatus.RequiresReview, [], owner,
            DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMinutes(-10), null, null, null, null,
            Guid.NewGuid().ToString("N"));

    private sealed class StubAgentWorkItemRepository : IAgentWorkItemRepository
    {
        private readonly List<AgentWorkItem> items = [];
        public Task<IReadOnlyList<AgentWorkItem>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentWorkItem>>(items.Where(item => item.Scope == scope).ToArray());
        public Task<AgentWorkItem?> GetAsync(Guid workItemId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.FirstOrDefault(item => item.WorkItemId == workItemId && item.Scope == scope));
        public Task<AgentWorkItem?> GetForNodeAsync(Guid runId, string nodeKey, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.FirstOrDefault(item => item.RunId == runId && item.NodeKey == nodeKey && item.Scope == scope));
        public Task SaveAsync(AgentWorkItem workItem, CancellationToken cancellationToken = default)
        {
            items.RemoveAll(item => item.WorkItemId == workItem.WorkItemId);
            items.Add(workItem);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPermissionService(params Permission[] permissions) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissions.Contains(permission));
        public async Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!await HasPermissionAsync(permission, cancellationToken)) throw new PermissionDeniedException(permission);
        }
    }

    private sealed class StubAuditWriter : IAuditWriter
    {
        public AuditEvent? LastEvent { get; private set; }
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            LastEvent = auditEvent;
            return Task.CompletedTask;
        }
    }
}
