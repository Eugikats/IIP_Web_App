using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Api.Workflow;

public static class WorkflowEndpoints
{
    public static async Task<IResult> GetTimeline(
        Guid caseId,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        ICaseTimelineProvider timelineProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return NotFoundCase();
        }

        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        return Results.Ok(timelineProvider.Build(caseId, auditEvents).Select(WorkflowMapper.ToResponse).ToArray());
    }

    public static async Task<IResult> GetRecommendedActions(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IAgentActionLogRepository actionLogRepository,
        ISlaCalculator slaCalculator,
        IWorkflowRuleProvider workflowRuleProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var context = await BuildEvaluationContextAsync(
            caseId,
            scope,
            caseRepository,
            documentRepository,
            checklistProvider,
            actionLogRepository,
            slaCalculator,
            httpContext,
            cancellationToken);

        if (context is null)
        {
            return NotFoundCase();
        }

        return Results.Ok(workflowRuleProvider.GetRecommendedActions(context).Select(WorkflowMapper.ToResponse).ToArray());
    }

    public static async Task<IResult> EvaluateWorkflow(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IAgentActionLogRepository actionLogRepository,
        ISlaCalculator slaCalculator,
        IWorkflowRuleProvider workflowRuleProvider,
        IWorkflowTaskRepository taskRepository,
        IApprovalRepository approvalRepository,
        IAuditRepository auditRepository,
        INotificationEmitter notificationEmitter,
        IMissingDocumentRequestService missingDocumentRequestService,
        IWhatsAppRuntimeSettingsProvider whatsAppSettingsProvider,
        ILogger<MissingDocumentRequestLogMarker> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var context = await BuildEvaluationContextAsync(
            caseId,
            scope,
            caseRepository,
            documentRepository,
            checklistProvider,
            actionLogRepository,
            slaCalculator,
            httpContext,
            cancellationToken);

        if (context is null)
        {
            return NotFoundCase();
        }

        if (!CaseActions.CanEvaluateWorkflow(context.ClaimCase, out var blockedReason))
        {
            var blockedAction = CaseActions.Blocked(context.ClaimCase).First(action => action.Action == CaseActions.EvaluateCaseWorkflow);
            return Results.Conflict(new
            {
                message = blockedReason,
                blockedAction
            });
        }

        var correlationId = GetCorrelationId(httpContext);
        var actor = GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Validation("userId", "Authenticated user context is required to evaluate a case workflow.");
        }

        var actions = workflowRuleProvider.GetRecommendedActions(context);
        var beforeCase = context.ClaimCase;
        var routing = ClaimRoutingPolicy.Evaluate(beforeCase);
        var nextStatus = ResolveNextStatus(beforeCase, actions);
        var claimForWorkflow = beforeCase;
        if (nextStatus != beforeCase.Status
            || (nextStatus == CaseStatus.Escalated && !string.Equals(beforeCase.AssignedTeam, ClaimRoutingPolicy.ResolveEscalationTeam(routing), StringComparison.OrdinalIgnoreCase)))
        {
            claimForWorkflow = beforeCase with
            {
                Status = nextStatus,
                AssignedTeam = nextStatus == CaseStatus.Escalated
                    ? ClaimRoutingPolicy.ResolveEscalationTeam(routing)
                    : beforeCase.AssignedTeam,
                Priority = nextStatus == CaseStatus.Escalated
                    ? (routing.Priority == CasePriority.Normal ? CasePriority.Urgent : routing.Priority)
                    : beforeCase.Priority,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await caseRepository.SaveAsync(claimForWorkflow, cancellationToken);
            await auditRepository.WriteAsync(
                AuditEvent.CaseStatusUpdated(
                    beforeCase.ToAuditSnapshot(),
                    claimForWorkflow.ToAuditSnapshot(),
                    actor,
                    correlationId,
                    nextStatus == CaseStatus.Escalated
                        ? routing.OperatorExplanation
                        : "Workflow evaluation updated case lifecycle status."),
                cancellationToken);

            if (nextStatus == CaseStatus.Escalated)
            {
                await notificationEmitter.NotifyAsync(
                    claimForWorkflow.Scope,
                    claimForWorkflow.AssignedUserId ?? string.Empty,
                    NotificationType.ClaimEscalated,
                    "This claim was automatically escalated.",
                    claimForWorkflow.CaseId,
                    cancellationToken);
            }

            // WO 5.4: the moment a case actually transitions INTO MissingDocuments, not every
            // time workflow evaluation merely confirms it is still there, tell the customer what
            // is missing, over the channel the claim itself arrived on. A failed or blocked
            // request must never fail workflow evaluation, which already succeeded above.
            if (nextStatus == CaseStatus.MissingDocuments && beforeCase.Status != CaseStatus.MissingDocuments)
            {
                try
                {
                    await missingDocumentRequestService.SendAsync(
                        claimForWorkflow,
                        context.Checklist,
                        correlationId,
                        whatsAppSettingsProvider.ForScope(claimForWorkflow.Scope)?.PhoneNumberId,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Missing-document request failed for case {CaseId}", claimForWorkflow.CaseId);
                }
            }
        }

        await ClaimStatusNotifications.NotifyAsync(httpContext, beforeCase, claimForWorkflow, correlationId, cancellationToken);

        var createdTasks = new List<WorkflowTask>();
        var createdApprovals = new List<ApprovalRequest>();

        await auditRepository.WriteAsync(AuditEvent.WorkflowEvaluated(claimForWorkflow.ToAuditSnapshot(), actor, correlationId), cancellationToken);

        foreach (var action in actions)
        {
            var task = CreateTask(claimForWorkflow, action, actor, correlationId);
            await taskRepository.SaveAsync(task, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.WorkflowTaskCreated(task), cancellationToken);
            createdTasks.Add(task);

            if (action.RequiresHumanApproval)
            {
                var approval = CreateApproval(claimForWorkflow, action, actor, correlationId);
                await approvalRepository.SaveAsync(approval, cancellationToken);
                await auditRepository.WriteAsync(AuditEvent.ApprovalCreated(approval), cancellationToken);
                createdApprovals.Add(approval);
            }
        }

        return Results.Ok(new WorkflowEvaluationResponse(
            caseId,
            context.SlaStatus.ToString(),
            actions.Select(WorkflowMapper.ToResponse).ToArray(),
            createdTasks.Select(WorkflowMapper.ToResponse).ToArray(),
            createdApprovals.Select(WorkflowMapper.ToResponse).ToArray()));
    }

    private static CaseStatus ResolveNextStatus(ClaimCase claimCase, IReadOnlyCollection<RecommendedNextAction> actions)
    {
        if (claimCase.Status is CaseStatus.Settled or CaseStatus.Closed or CaseStatus.Escalated)
        {
            return claimCase.Status;
        }

        if (actions.Any(IsDesignatedHumanEscalationAction))
        {
            return CaseStatus.Escalated;
        }

        if (actions.Any(action => action.ActionType == RecommendedNextActionType.SupervisorReview && action.RequiresHumanApproval))
        {
            return CaseStatus.PendingApproval;
        }

        if (actions.Any(action => action.ActionType == RecommendedNextActionType.RequestMissingDocuments))
        {
            return CaseStatus.MissingDocuments;
        }

        if (actions.Any(action => action.ActionType == RecommendedNextActionType.MarkReadyForReview))
        {
            return CaseStatus.ReadyForReview;
        }

        return claimCase.Status == CaseStatus.New ? CaseStatus.InReview : claimCase.Status;
    }

    private static bool IsDesignatedHumanEscalationAction(RecommendedNextAction action) =>
        action.ActionType == RecommendedNextActionType.SupervisorReview
        && action.RequiresHumanApproval
        && (action.Label.Contains("Designated", StringComparison.OrdinalIgnoreCase)
            || action.Evidence.Any(item => Enum.TryParse<ClaimEscalationTrigger>(item, ignoreCase: true, out _)));

    public static async Task<IResult> ListTasks(
        IWorkflowTaskRepository taskRepository,
        ICaseRepository caseRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var tasks = await taskRepository.ListAsync(scope, cancellationToken);
        var visibleTasks = new List<WorkflowTask>();
        foreach (var task in tasks)
        {
            if (await GetScopedCaseAsync(caseRepository, task.CaseId, scope, httpContext, cancellationToken) is not null)
            {
                visibleTasks.Add(task);
            }
        }

        return Results.Ok(visibleTasks.Select(WorkflowMapper.ToResponse).ToArray());
    }

    public static async Task<IResult> GetTask(
        Guid taskId,
        IWorkflowTaskRepository taskRepository,
        ICaseRepository caseRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var task = await taskRepository.GetAsync(taskId, scope, cancellationToken);
        if (task is null
            || await GetScopedCaseAsync(caseRepository, task.CaseId, scope, httpContext, cancellationToken) is null)
        {
            return Results.NotFound(new { message = "Workflow task was not found for the requested tenant workspace." });
        }

        return Results.Ok(WorkflowMapper.ToResponse(task));
    }

    public static async Task<IResult> AssignTask(
        Guid taskId,
        AssignWorkflowTaskRequest request,
        IWorkflowTaskRepository taskRepository,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        INotificationEmitter notificationEmitter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var task = await taskRepository.GetAsync(taskId, scope, cancellationToken);
        if (task is null
            || await GetScopedCaseAsync(caseRepository, task.CaseId, scope, httpContext, cancellationToken) is null)
        {
            return Results.NotFound(new { message = "Workflow task was not found for the requested tenant workspace." });
        }

        var assignedBy = GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(assignedBy))
        {
            return Validation("userId", "Authenticated user context is required to assign a workflow task.");
        }

        var updated = task with
        {
            Status = WorkflowTaskStatus.InProgress,
            AssignedUserId = Normalize(request.AssignedUserId),
            AssignedTeam = Normalize(request.AssignedTeam) ?? task.AssignedTeam,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await taskRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.WorkflowTaskAssigned(updated, assignedBy, task.Status.ToString()), cancellationToken);

        // WO-14: only the approval-flavored task types are "an approval assigned", other task
        // types (document requests, case review, etc.) are out of this notification's scope.
        if (updated.TaskType is WorkflowTaskType.SupervisorApproval or WorkflowTaskType.CustomerUpdateApproval)
        {
            await notificationEmitter.NotifyAsync(
                updated.Scope,
                updated.AssignedUserId ?? string.Empty,
                NotificationType.ApprovalAssigned,
                "You were assigned an approval task.",
                updated.CaseId,
                cancellationToken);
        }

        return Results.Ok(WorkflowMapper.ToResponse(updated));
    }

    public static async Task<IResult> CompleteTask(
        Guid taskId,
        CompleteWorkflowTaskRequest request,
        IWorkflowTaskRepository taskRepository,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var task = await taskRepository.GetAsync(taskId, scope, cancellationToken);
        if (task is null
            || await GetScopedCaseAsync(caseRepository, task.CaseId, scope, httpContext, cancellationToken) is null)
        {
            return Results.NotFound(new { message = "Workflow task was not found for the requested tenant workspace." });
        }

        var completedBy = GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(completedBy))
        {
            return Validation("userId", "Authenticated user context is required to complete a workflow task.");
        }

        var updated = task with
        {
            Status = WorkflowTaskStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await taskRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.WorkflowTaskCompleted(updated, completedBy, task.Status.ToString()), cancellationToken);

        return Results.Ok(WorkflowMapper.ToResponse(updated));
    }

    private static async Task<WorkflowEvaluationContext?> BuildEvaluationContextAsync(
        Guid caseId,
        TenantScope scope,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IAgentActionLogRepository actionLogRepository,
        ISlaCalculator slaCalculator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var claimCase = await GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return null;
        }

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var agentActions = await actionLogRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var slaStatus = slaCalculator.Calculate(claimCase, DateTimeOffset.UtcNow);

        return new WorkflowEvaluationContext(claimCase, checklist, agentActions, slaStatus);
    }

    private static WorkflowTask CreateTask(ClaimCase claimCase, RecommendedNextAction action, string actor, string correlationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowTask
        {
            TaskId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            TaskType = ToTaskType(action.ActionType),
            Status = WorkflowTaskStatus.Open,
            Title = action.Label,
            Summary = action.Summary,
            AssignedTeam = action.ActionType == RecommendedNextActionType.SupervisorReview ? "Claims Supervision" : claimCase.AssignedTeam,
            CreatedByUserId = actor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };
    }

    private static ApprovalRequest CreateApproval(ClaimCase claimCase, RecommendedNextAction action, string actor, string correlationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            ApprovalType = action.ActionType.ToString(),
            Status = ApprovalStatus.Pending,
            RiskLevel = action.Priority,
            EvidenceSummary = action.Summary,
            SourceReferences = action.Evidence,
            RequestedByUserId = actor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };
    }

    private static WorkflowTaskType ToTaskType(RecommendedNextActionType actionType) =>
        actionType switch
        {
            RecommendedNextActionType.RequestMissingDocuments => WorkflowTaskType.RequestMissingDocuments,
            RecommendedNextActionType.SupervisorReview => WorkflowTaskType.SupervisorApproval,
            RecommendedNextActionType.HumanReview => WorkflowTaskType.ComplianceReview,
            RecommendedNextActionType.FollowUpCustomer => WorkflowTaskType.FollowUpCustomer,
            _ => WorkflowTaskType.ReviewCase
        };

    internal static async Task<ClaimCase?> GetScopedCaseAsync(
        ICaseRepository caseRepository,
        Guid caseId,
        TenantScope scope,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        return claimCase is not null
            && claimCase.Scope.Matches(scope)
            && ClaimAccessGuard.CanViewCase(httpContext, claimCase)
            ? claimCase
            : null;
    }

    internal static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    internal static IResult HumanDecisionRequired(string message) =>
        Results.Json(
            new { message, code = "human_decision_required" },
            statusCode: StatusCodes.Status403Forbidden);

    internal static IResult NotFoundCase() =>
        Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });

    internal static string GetActor(HttpContext httpContext, string? candidate)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } tenantContext
            && !string.IsNullOrWhiteSpace(tenantContext.Current.UserId))
        {
            return tenantContext.Current.UserId;
        }

        var runtimeActor = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (!string.IsNullOrWhiteSpace(runtimeActor))
        {
            return runtimeActor;
        }

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(candidate) ? candidate.Trim() : "local-workflow-user";
    }

    internal static bool TryResolveScope(
        HttpContext httpContext,
        out TenantScope scope,
        out string userId,
        out string validationError)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } tenantContext)
        {
            var current = tenantContext.Current;
            scope = TenantScope.Create(current.TenantId, current.WorkspaceId);
            userId = current.UserId;
            validationError = string.Empty;
            return true;
        }

        if (TenantScopeReader.TryGetFromHeaders(httpContext, out scope, out validationError))
        {
            userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
            return true;
        }

        userId = string.Empty;
        return false;
    }

    internal static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Empty marker type purely so ILogger&lt;T&gt; has a stable category name for this
/// static-class endpoint (ILogger&lt;WorkflowEndpoints&gt; is not usable, the class is static),
/// matching SmsIntegrationEndpoints' SmsDeliveryStatusLogMarker precedent.</summary>
public sealed class MissingDocumentRequestLogMarker;
