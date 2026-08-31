using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.MyWork;

public sealed class MyWorkService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IWorkflowTaskRepository taskRepository,
    IApprovalRepository approvalRepository,
    ICustomerUpdateRepository customerUpdateRepository,
    IAgentWorkItemRepository agentWorkItemRepository,
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAuditWriter auditWriter) : IMyWorkService
{
    public async Task<MyWorkResponse> ListAsync(MyWorkQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "My Work limit must be between 1 and 100.");
        }

        var permissions = await ResolvePermissionsAsync(cancellationToken);
        if (!permissions.AnySupported)
        {
            throw new PermissionDeniedException(Permission.ViewAssignedClaims);
        }

        if (query.Ownership is MyWorkOwnership.All or MyWorkOwnership.Unassigned && !permissions.ViewAllClaims)
        {
            throw new PermissionDeniedException(Permission.ViewAllClaims);
        }

        var context = tenantContextAccessor.Current;
        var allCases = permissions.CanReadClaims
            ? await caseRepository.ListAsync(context.Scope, cancellationToken)
            : [];
        var visibleCases = allCases
            .Where(claimCase => permissions.ViewAllClaims || IsCaseVisibleToUser(claimCase, context.UserId))
            .ToDictionary(claimCase => claimCase.CaseId);
        var candidates = new List<MyWorkItemDto>();

        // Protected repositories share one scoped DbContext. Keep these reads sequential.
        if (permissions.CanReadClaims)
        {
            await AddClaimOwnedWorkAsync(candidates, visibleCases, context.UserId, context.Scope, cancellationToken);
        }

        if (permissions.ApproveWork)
        {
            AddApprovals(candidates, await approvalRepository.ListAsync(context.Scope, cancellationToken), visibleCases);
        }

        if (permissions.ReviewAgentWork)
        {
            AddAgentWork(candidates, await agentWorkItemRepository.ListAsync(context.Scope, cancellationToken), visibleCases);
        }

        var filtered = candidates
            .Where(item => MatchesOwnership(item, query.Ownership, context.UserId))
            .Where(item => query.SourceType is null || item.SourceType == query.SourceType)
            .Where(item => query.State is null || item.State == query.State)
            .OrderBy(item => DueRank(item.DueState))
            .ThenBy(item => StateRank(item.State))
            .ThenBy(item => PriorityRank(item.Priority))
            .ThenBy(item => item.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var items = filtered.Take(query.Limit).ToArray();
        var summary = new MyWorkSummary(
            filtered.Length,
            filtered.Count(item => item.DueState == MyWorkDueState.Overdue),
            filtered.Count(item => string.IsNullOrWhiteSpace(item.OwnerUserId)),
            filtered.Count(item => item.ApprovalRequired));

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, null, null, context.UserId, ActorType.User,
                "my-work.viewed", DateTimeOffset.UtcNow, null, null,
                items.Select(item => item.Id).ToArray(), "AuthoritativeWorkProjection", null,
                $"ownership:{query.Ownership};returned:{items.Length};has-more:{filtered.Length > query.Limit}",
                null, "Permission-filtered My Work projection viewed; source content omitted.",
                Guid.NewGuid().ToString("N")),
            cancellationToken);

        return new MyWorkResponse(query.Ownership, items.Length, filtered.Length > query.Limit, items, summary);
    }

    private async Task AddClaimOwnedWorkAsync(
        List<MyWorkItemDto> items,
        IReadOnlyDictionary<Guid, ClaimCase> visibleCases,
        string userId,
        Domain.Common.TenantScope scope,
        CancellationToken cancellationToken)
    {
        var tasks = await taskRepository.ListAsync(scope, cancellationToken);
        foreach (var task in tasks.Where(task => visibleCases.ContainsKey(task.CaseId)))
        {
            items.Add(FromTask(task, visibleCases[task.CaseId]));
        }

        foreach (var claimCase in visibleCases.Values)
        {
            var documents = await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            foreach (var document in documents.Where(document => document.ProcessingStatus == DocumentProcessingStatus.ReviewRequired))
            {
                items.Add(FromDocument(document, claimCase));
            }
        }

        if (await permissionService.HasPermissionAsync(Permission.ManageCustomerCommunications, cancellationToken))
        {
            var updates = await customerUpdateRepository.ListAsync(scope, cancellationToken);
            foreach (var update in updates.Where(update => visibleCases.ContainsKey(update.CaseId) && IsActionable(update.Status)))
            {
                items.Add(FromCustomerUpdate(update, visibleCases[update.CaseId], userId));
            }
        }
    }

    private static void AddApprovals(
        List<MyWorkItemDto> items,
        IEnumerable<ApprovalRequest> approvals,
        IReadOnlyDictionary<Guid, ClaimCase> visibleCases)
    {
        foreach (var approval in approvals)
        {
            visibleCases.TryGetValue(approval.CaseId, out var claimCase);
            items.Add(FromApproval(approval, claimCase));
        }
    }

    private static void AddAgentWork(
        List<MyWorkItemDto> items,
        IEnumerable<AgentWorkItem> agentItems,
        IReadOnlyDictionary<Guid, ClaimCase> visibleCases)
    {
        foreach (var agentItem in agentItems)
        {
            ClaimCase? claimCase = null;
            if (Guid.TryParse(agentItem.BusinessReferenceId, out var caseId))
            {
                visibleCases.TryGetValue(caseId, out claimCase);
            }
            items.Add(FromAgentWorkItem(agentItem, claimCase));
        }
    }

    private async Task<ResolvedPermissions> ResolvePermissionsAsync(CancellationToken cancellationToken)
    {
        var viewAll = await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken);
        var viewAssigned = await permissionService.HasPermissionAsync(Permission.ViewAssignedClaims, cancellationToken);
        var approve = await permissionService.HasPermissionAsync(Permission.ApproveSettlementWorkflowStep, cancellationToken);
        var reviewAgent = await permissionService.HasPermissionAsync(Permission.ReviewAgentWorkItems, cancellationToken);
        var communications = await permissionService.HasPermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        return new ResolvedPermissions(viewAll, viewAssigned, approve, reviewAgent, communications);
    }

    private static MyWorkItemDto FromTask(WorkflowTask task, ClaimCase claimCase) =>
        Item(
            MyWorkSourceType.WorkflowTask, task.TaskId, task.CaseId, task.Title,
            task.Status == WorkflowTaskStatus.InProgress ? "Continue task" : "Open task",
            task.Status.ToString(), task.Status switch
            {
                WorkflowTaskStatus.Open => MyWorkState.Ready,
                WorkflowTaskStatus.InProgress => MyWorkState.InProgress,
                WorkflowTaskStatus.Completed => MyWorkState.Completed,
                _ => MyWorkState.Closed
            },
            task.AssignedUserId, task.AssignedTeam, "Workflow task assignment", claimCase,
            claimCase.CompletenessScore < 100 ? MyWorkEvidenceReadiness.Gaps : MyWorkEvidenceReadiness.Ready,
            false, $"/app/claims/{task.CaseId}?taskId={task.TaskId}", task.UpdatedAt);

    private static MyWorkItemDto FromApproval(ApprovalRequest approval, ClaimCase? claimCase) =>
        Item(
            MyWorkSourceType.Approval, approval.ApprovalId, approval.CaseId, approval.ApprovalType,
            approval.Status == ApprovalStatus.Escalated ? "Resolve escalation" : "Review exact proposed action",
            approval.Status.ToString(), approval.Status switch
            {
                ApprovalStatus.Pending => MyWorkState.Ready,
                ApprovalStatus.Escalated => MyWorkState.Escalated,
                _ => MyWorkState.Completed
            },
            approval.ReviewerUserId, null, "Approval reviewer", claimCase,
            approval.SourceReferences.Count == 0 ? MyWorkEvidenceReadiness.Gaps : MyWorkEvidenceReadiness.Ready,
            true, $"/app/work-items?approvalId={approval.ApprovalId}", approval.UpdatedAt,
            approval.RiskLevel);

    private static MyWorkItemDto FromDocument(CaseDocument document, ClaimCase claimCase) =>
        Item(
            MyWorkSourceType.DocumentReview, document.DocumentId, document.CaseId,
            $"Review {document.OriginalFileName}", "Review extracted fields and source evidence",
            document.ProcessingStatus.ToString(), MyWorkState.Ready,
            claimCase.AssignedUserId, claimCase.AssignedTeam, "Case assignment", claimCase,
            document.ValidationWarnings.Count > 0 ? MyWorkEvidenceReadiness.Gaps : MyWorkEvidenceReadiness.Ready,
            false, $"/app/claims/{document.CaseId}/extraction?documentId={document.DocumentId}",
            document.ProcessedAt ?? document.UploadedAt);

    private static MyWorkItemDto FromCustomerUpdate(CustomerUpdateDraft update, ClaimCase claimCase, string userId) =>
        Item(
            MyWorkSourceType.CommunicationReview, update.UpdateId, update.CaseId,
            $"Review {update.Channel} customer update", update.Status == CustomerUpdateStatus.Approved ? "Send approved update" : "Review customer-safe draft",
            update.Status.ToString(), update.Status == CustomerUpdateStatus.Approved ? MyWorkState.InProgress : MyWorkState.Ready,
            claimCase.AssignedUserId ?? (MatchesIdentity(update.CreatedByUserId, userId) ? update.CreatedByUserId : null),
            claimCase.AssignedTeam, claimCase.AssignedUserId is null ? "Draft creator" : "Case assignment", claimCase,
            MyWorkEvidenceReadiness.NotAssessed, update.RequiresApproval,
            $"/app/communications?updateId={update.UpdateId}", update.UpdatedAt);

    private static MyWorkItemDto FromAgentWorkItem(AgentWorkItem item, ClaimCase? claimCase) =>
        Item(
            MyWorkSourceType.AgentWorkItem, item.WorkItemId,
            Guid.TryParse(item.BusinessReferenceId, out var caseId) ? caseId : null,
            item.Title, "Review prepared artifact and record a human handoff", item.Status.ToString(), item.Status switch
            {
                AgentWorkItemStatus.RequiresReview => MyWorkState.Ready,
                AgentWorkItemStatus.InProgress => MyWorkState.InProgress,
                AgentWorkItemStatus.Escalated => MyWorkState.Escalated,
                AgentWorkItemStatus.Completed or AgentWorkItemStatus.ReturnedForCorrection => MyWorkState.Completed,
                _ => MyWorkState.Closed
            },
            item.AssignedToUserId, null, "Agent work-item assignment", claimCase,
            item.Evidence.Count == 0 ? MyWorkEvidenceReadiness.Gaps : MyWorkEvidenceReadiness.Ready,
            false, $"/app/work-items?agentWorkItemId={item.WorkItemId}", item.UpdatedAt);

    private static MyWorkItemDto Item(
        MyWorkSourceType type,
        Guid sourceId,
        Guid? caseId,
        string title,
        string requiredAction,
        string sourceStatus,
        MyWorkState state,
        string? ownerUserId,
        string? ownerTeam,
        string ownerBasis,
        ClaimCase? claimCase,
        MyWorkEvidenceReadiness evidence,
        bool approvalRequired,
        string href,
        DateTimeOffset updatedAt,
        string? priority = null)
    {
        var dueAt = claimCase?.SlaDueAt;
        return new MyWorkItemDto(
            $"{type}:{sourceId}", type, sourceId.ToString(), caseId?.ToString(), title, requiredAction,
            sourceStatus, state, Normalize(ownerUserId), Normalize(ownerTeam), ownerBasis,
            priority ?? claimCase?.Priority.ToString() ?? "Normal", dueAt, DueState(dueAt), evidence,
            approvalRequired, href, updatedAt);
    }

    private static bool MatchesOwnership(MyWorkItemDto item, MyWorkOwnership ownership, string userId) => ownership switch
    {
        MyWorkOwnership.Mine => MatchesIdentity(item.OwnerUserId, userId),
        MyWorkOwnership.Unassigned => string.IsNullOrWhiteSpace(item.OwnerUserId),
        _ => true
    };

    private static bool IsCaseVisibleToUser(ClaimCase claimCase, string userId) =>
        MatchesIdentity(claimCase.AssignedUserId, userId) || MatchesIdentity(claimCase.CreatedByUserId, userId);

    private static bool MatchesIdentity(string? candidate, string userId) =>
        !string.IsNullOrWhiteSpace(candidate) && string.Equals(candidate.Trim(), userId.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsActionable(CustomerUpdateStatus status) =>
        status is CustomerUpdateStatus.Draft or CustomerUpdateStatus.PendingApproval or CustomerUpdateStatus.Approved;

    private static MyWorkDueState DueState(DateTimeOffset? dueAt)
    {
        if (dueAt is null) return MyWorkDueState.NoDueDate;
        var remaining = dueAt.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return MyWorkDueState.Overdue;
        return remaining <= TimeSpan.FromHours(24) ? MyWorkDueState.DueSoon : MyWorkDueState.OnTrack;
    }

    private static int DueRank(MyWorkDueState value) => value switch
    {
        MyWorkDueState.Overdue => 0,
        MyWorkDueState.DueSoon => 1,
        MyWorkDueState.OnTrack => 2,
        _ => 3
    };

    private static int StateRank(MyWorkState value) => value switch
    {
        MyWorkState.Escalated => 0,
        MyWorkState.Ready => 1,
        MyWorkState.InProgress => 2,
        MyWorkState.Completed => 3,
        _ => 4
    };

    private static int PriorityRank(string value) => value.ToLowerInvariant() switch
    {
        "critical" => 0,
        "urgent" => 1,
        "high" => 2,
        "medium" => 3,
        "normal" => 4,
        "low" => 5,
        _ => 6
    };

    private sealed record ResolvedPermissions(
        bool ViewAllClaims,
        bool ViewAssignedClaims,
        bool ApproveWork,
        bool ReviewAgentWork,
        bool ManageCommunications)
    {
        public bool CanReadClaims => ViewAllClaims || ViewAssignedClaims;
        public bool AnySupported => CanReadClaims || ApproveWork || ReviewAgentWork || ManageCommunications;
    }
}
