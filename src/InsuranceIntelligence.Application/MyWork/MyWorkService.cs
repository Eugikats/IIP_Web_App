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
    IAuditWriter auditWriter,
    ITenantConfigurationRepository? tenantConfigurationRepository = null) : IMyWorkService
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
            var confidenceThreshold = tenantConfigurationRepository is null
                ? 0.8m
                : (await tenantConfigurationRepository.GetAsync(context.Scope, cancellationToken)).DocumentClassificationConfidenceThreshold;
            await AddClaimOwnedWorkAsync(candidates, visibleCases, context.UserId, context.Scope, confidenceThreshold, cancellationToken);
        }

        if (permissions.ApproveWork)
        {
            AddApprovals(candidates, await approvalRepository.ListAsync(context.Scope, cancellationToken), visibleCases);
        }

        if (permissions.ReviewAgentWork)
        {
            AddAgentWork(candidates, await agentWorkItemRepository.ListAsync(context.Scope, cancellationToken), visibleCases);
        }

        var now = DateTimeOffset.UtcNow;
        var filtered = candidates
            .Where(item => MatchesOwnership(item, query.Ownership, context.UserId))
            .Where(item => query.SourceType is null || item.SourceType == query.SourceType)
            .Where(item => query.State is null || item.State == query.State)
            .Select(item => (Item: item, Score: MyWorkRanking.Explain(
                item.DueState, item.EvidenceReadiness, item.ApprovalRequired, item.State, item.Priority,
                item.OwnerUserId is not null, item.UpdatedAt, now).Score))
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Item.UpdatedAt)
            .ThenBy(pair => pair.Item.Id, StringComparer.Ordinal)
            .Select(pair => pair.Item)
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
        decimal confidenceThreshold,
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
            foreach (var document in documents.Where(document => NeedsReview(document, confidenceThreshold)))
            {
                items.Add(FromDocument(document, claimCase, confidenceThreshold));
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

    // A document is not reliable evidence until a person has looked at it: not yet approved, and
    // either the classifier itself was unsure (confidence below this tenant's configured
    // threshold, or it landed on Unknown outright) or extraction raised a validation warning.
    // Mirrors InsuranceIntelligence.Api.Documents.DocumentMapper.ApproveExtractionBlockedReason's
    // conditions; duplicated rather than referenced because Application cannot depend on Api
    // (same reasoning as the Functions/Api split elsewhere in this codebase).
    private static bool NeedsReview(CaseDocument document, decimal confidenceThreshold) =>
        !document.Extraction.Approved
        && document.ProcessingStatus is DocumentProcessingStatus.Processed or DocumentProcessingStatus.ReviewRequired
        && (document.DocumentType == DocumentType.Unknown
            || document.Classification.ConfidenceScore < confidenceThreshold
            || document.ValidationWarnings.Count > 0);

    // Outcome K acceptance: "a low confidence document lands in a person's queue with the reason
    // shown" -- names the actual confidence/threshold/warning, not a generic sentence.
    private static string ReviewReason(CaseDocument document, decimal confidenceThreshold)
    {
        if (document.DocumentType == DocumentType.Unknown)
        {
            return "The classifier could not identify this document's type.";
        }

        var reasons = new List<string>();
        if (document.Classification.ConfidenceScore < confidenceThreshold)
        {
            reasons.Add($"classified as {document.DocumentType} at {document.Classification.ConfidenceScore:P0} confidence, below the {confidenceThreshold:P0} review threshold");
        }

        if (document.ValidationWarnings.Count > 0)
        {
            reasons.Add(document.ValidationWarnings.Count == 1
                ? $"1 validation warning ({document.ValidationWarnings.First().Message})"
                : $"{document.ValidationWarnings.Count} validation warnings");
        }

        return reasons.Count == 0
            ? "This document needs a person to verify it before it can be used as evidence."
            : $"Needs review: {string.Join(", ", reasons)}.";
    }

    private static MyWorkItemDto FromDocument(CaseDocument document, ClaimCase claimCase, decimal confidenceThreshold)
    {
        var item = Item(
            MyWorkSourceType.DocumentReview, document.DocumentId, document.CaseId,
            $"Review {document.OriginalFileName}", "Review extracted fields and source evidence",
            document.ProcessingStatus.ToString(), MyWorkState.Ready,
            claimCase.AssignedUserId, claimCase.AssignedTeam, "Case assignment", claimCase,
            document.ValidationWarnings.Count > 0 ? MyWorkEvidenceReadiness.Gaps : MyWorkEvidenceReadiness.Ready,
            false, $"/app/claims/{document.CaseId}/extraction?documentId={document.DocumentId}",
            document.ProcessedAt ?? document.UploadedAt);

        // The generic ranking reason (MyWorkRanking.Explain) has no document-specific fields to
        // draw on; outcome K's acceptance needs the actual confidence number and threshold named,
        // so this overrides it with that specific reason for document-review items only.
        return item with { Reason = ReviewReason(document, confidenceThreshold) };
    }

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
        var dueState = DueState(dueAt);
        var normalizedOwner = Normalize(ownerUserId);
        var resolvedPriority = priority ?? claimCase?.Priority.ToString() ?? "Normal";
        var caseReference = claimCase is null ? null : $"{claimCase.PolicyNumber} - {claimCase.CustomerName}";
        var (_, reason) = MyWorkRanking.Explain(
            dueState, evidence, approvalRequired, state, resolvedPriority,
            normalizedOwner is not null, updatedAt, DateTimeOffset.UtcNow);

        return new MyWorkItemDto(
            $"{type}:{sourceId}", type, sourceId.ToString(), caseId?.ToString(), title, requiredAction,
            sourceStatus, state, normalizedOwner, Normalize(ownerTeam), ownerBasis,
            resolvedPriority, dueAt, dueState, evidence,
            approvalRequired, href, updatedAt, caseReference, reason);
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
