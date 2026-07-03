using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Workflow;

public static class ApprovalEndpoints
{
    public static async Task<IResult> ListApprovals(
        IApprovalRepository approvalRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var approvals = await approvalRepository.ListAsync(scope, cancellationToken);
        return Results.Ok(approvals.Select(WorkflowMapper.ToResponse).ToArray());
    }

    public static async Task<IResult> GetApproval(
        Guid approvalId,
        IApprovalRepository approvalRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var approval = await approvalRepository.GetAsync(approvalId, scope, cancellationToken);
        return approval is null
            ? Results.NotFound(new { message = "Approval request was not found for the requested tenant workspace." })
            : Results.Ok(WorkflowMapper.ToResponse(approval));
    }

    public static async Task<IResult> CreateApproval(
        Guid caseId,
        CreateApprovalRequest request,
        ICaseRepository caseRepository,
        IApprovalRepository approvalRepository,
        IAuditRepository auditRepository,
        ITeamsNotificationQueue teamsQueue,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        if (!CaseActions.CanCreateApproval(claimCase, out var blockedReason))
        {
            var blockedAction = CaseActions.Blocked(claimCase).First(action => action.Action == CaseActions.CreateApprovalRequest);
            return Results.Conflict(new
            {
                message = blockedReason,
                blockedAction
            });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to create an approval request.");
        }

        var now = DateTimeOffset.UtcNow;
        var approval = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ApprovalType = Normalize(request.ApprovalType) ?? "WorkflowReview",
            Status = ApprovalStatus.Pending,
            RiskLevel = Normalize(request.RiskLevel) ?? claimCase.RiskLevel,
            EvidenceSummary = Normalize(request.EvidenceSummary) ?? "Human-supervised workflow approval requested.",
            SourceReferences = request.SourceReferences?.Where(source => !string.IsNullOrWhiteSpace(source)).Select(source => source.Trim()).ToArray() ?? Array.Empty<string>(),
            RequestedByUserId = actor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = WorkflowEndpoints.GetCorrelationId(httpContext)
        };

        await approvalRepository.SaveAsync(approval, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ApprovalCreated(approval), cancellationToken);

        await teamsQueue.EnqueueAsync(new TeamsNotification
        {
            Scope = scope,
            CaseId = caseId,
            NotificationType = TeamsNotificationType.ApprovalRequired,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                approval.ApprovalId,
                approval.ApprovalType,
                approval.RiskLevel,
                approval.EvidenceSummary,
                approval.RequestedByUserId
            })
        }, cancellationToken);

        return Results.Created($"/api/approvals/{approval.ApprovalId}", WorkflowMapper.ToResponse(approval));
    }

    public static Task<IResult> ApproveApproval(
        Guid approvalId,
        ApprovalDecisionRequest request,
        IApprovalRepository approvalRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        DecideAsync(approvalId, request, ApprovalStatus.Approved, ApprovalDecision.Approved, "approveApproval", approvalRepository, auditRepository, httpContext, cancellationToken);

    public static Task<IResult> RejectApproval(
        Guid approvalId,
        ApprovalDecisionRequest request,
        IApprovalRepository approvalRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        DecideAsync(approvalId, request, ApprovalStatus.Rejected, ApprovalDecision.Rejected, "rejectApproval", approvalRepository, auditRepository, httpContext, cancellationToken);

    public static Task<IResult> OverrideApproval(
        Guid approvalId,
        ApprovalDecisionRequest request,
        IApprovalRepository approvalRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        DecideAsync(approvalId, request, ApprovalStatus.Overridden, ApprovalDecision.Overridden, "overrideApproval", approvalRepository, auditRepository, httpContext, cancellationToken);

    public static Task<IResult> EscalateApproval(
        Guid approvalId,
        ApprovalDecisionRequest request,
        IApprovalRepository approvalRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        DecideAsync(approvalId, request, ApprovalStatus.Escalated, ApprovalDecision.Escalated, "escalateApproval", approvalRepository, auditRepository, httpContext, cancellationToken);

    private static async Task<IResult> DecideAsync(
        Guid approvalId,
        ApprovalDecisionRequest request,
        ApprovalStatus status,
        ApprovalDecision decision,
        string actionName,
        IApprovalRepository approvalRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var approval = await approvalRepository.GetAsync(approvalId, scope, cancellationToken);
        if (approval is null)
        {
            return Results.NotFound(new { message = "Approval request was not found for the requested tenant workspace." });
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            var blockedAction = ApprovalActions.Blocked(approval).FirstOrDefault(action => action.Action == actionName);
            return Results.Conflict(new
            {
                message = blockedAction?.Reason ?? "This approval action is not currently allowed by the backend workflow state.",
                blockedAction
            });
        }

        if (string.IsNullOrWhiteSpace(request.Comments))
        {
            return WorkflowEndpoints.Validation("comments", "Reviewer comments are required for every approval decision.");
        }

        var reviewer = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(reviewer))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated reviewer context is required for an approval decision.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return WorkflowEndpoints.Validation("auditReason", "An audit reason is required for every approval decision.");
        }

        var updated = approval with
        {
            Status = status,
            Decision = decision,
            ReviewerUserId = reviewer,
            Comments = Normalize(request.Comments),
            DecidedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await approvalRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(ToAuditEvent(updated, reviewer, decision, auditReason), cancellationToken);

        return Results.Ok(WorkflowMapper.ToResponse(updated));
    }

    private static AuditEvent ToAuditEvent(ApprovalRequest approval, string reviewer, ApprovalDecision decision, string? auditReason) =>
        decision switch
        {
            ApprovalDecision.Rejected => AuditEvent.ApprovalRejected(approval, reviewer, auditReason),
            ApprovalDecision.Overridden => AuditEvent.ApprovalOverridden(approval, reviewer, auditReason),
            ApprovalDecision.Escalated => AuditEvent.ApprovalEscalated(approval, reviewer, auditReason),
            _ => AuditEvent.ApprovalApproved(approval, reviewer, auditReason)
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
