using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Cases;

public static class CaseEndpoints
{
    public static async Task<IResult> CreateCase(
        CreateCaseRequest request,
        CaseIntakeService service,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out var userId, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Authenticated user context is required to create a case."]
            });
        }

        try
        {
            var correlationId = GetCorrelationId(httpContext);
            var command = new CreateCaseCommand(
                scope,
                request.CustomerName ?? string.Empty,
                request.CustomerEmail,
                request.CustomerPhone,
                request.PolicyNumber ?? string.Empty,
                request.ProductLine ?? string.Empty,
                request.ClaimType ?? string.Empty,
                request.Channel ?? string.Empty,
                request.IncidentDate ?? default,
                request.IncidentDescription ?? string.Empty,
                userId,
                correlationId,
                request.MotorFnolDetails,
                request.MedicalFnolDetails);

            var claimCase = await service.CreateCaseAsync(command, cancellationToken);
            var auditEvents = await auditRepository.ListForCaseAsync(claimCase.CaseId, cancellationToken);
            var response = CaseMapper.ToResponse(claimCase, auditEvents);

            return Results.Created($"/api/v1/cases/{claimCase.CaseId}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            });
        }
    }

    public static async Task<IResult> GetCase(
        Guid caseId,
        CaseIntakeService service,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await service.GetCaseAsync(caseId, scope, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        if (!ClaimAccessGuard.CanViewCase(httpContext, claimCase))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        return Results.Ok(CaseMapper.ToResponse(claimCase, auditEvents));
    }

    public static async Task<IResult> AssignCase(
        Guid caseId,
        AssignCaseRequest request,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out var userId, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Authenticated user context is required to assign a case."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.AssignedUserId) && string.IsNullOrWhiteSpace(request.AssignedTeam))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = ["Assigned user or assigned team is required."]
            });
        }

        var claimCase = await GetScopedCaseAsync(caseId, caseRepository, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var before = claimCase.ToAuditSnapshot();
        var updated = claimCase with
        {
            Status = claimCase.Status == CaseStatus.New ? CaseStatus.Assigned : claimCase.Status,
            AssignedUserId = NormalizeOptional(request.AssignedUserId),
            AssignedTeam = NormalizeOptional(request.AssignedTeam) ?? claimCase.AssignedTeam,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var correlationId = GetCorrelationId(httpContext);

        await caseRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseAssigned(before, updated.ToAuditSnapshot(), userId, correlationId, NormalizeOptional(request.AuditReason)),
            cancellationToken);

        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        return Results.Ok(CaseMapper.ToResponse(updated, auditEvents));
    }

    public static async Task<IResult> EscalateCase(
        Guid caseId,
        EscalateCaseRequest request,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        INotificationEmitter notificationEmitter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out var userId, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Authenticated user context is required to escalate a case."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.AuditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["auditReason"] = ["Escalation reason is required."]
            });
        }

        var claimCase = await GetScopedCaseAsync(caseId, caseRepository, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        if (claimCase.Status is CaseStatus.Settled or CaseStatus.Closed)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Settled or closed cases cannot be escalated from the active work queue."]
            });
        }

        var before = claimCase.ToAuditSnapshot();
        var routing = ClaimRoutingPolicy.Evaluate(claimCase);
        var designatedTeam = NormalizeOptional(request.AssignedTeam)
            ?? (routing.RequiresDesignatedHumanEscalation
                ? ClaimRoutingPolicy.ResolveEscalationTeam(routing)
                : ClaimRoutingPolicy.ClaimsSupervisionTeam);
        var updated = claimCase with
        {
            Status = CaseStatus.Escalated,
            Priority = routing.Priority == CasePriority.Normal ? CasePriority.Urgent : routing.Priority,
            AssignedTeam = designatedTeam,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var correlationId = GetCorrelationId(httpContext);

        await caseRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseEscalated(before, updated.ToAuditSnapshot(), userId, correlationId, request.AuditReason.Trim()),
            cancellationToken);

        // Notify the officer already assigned to this claim — escalation doesn't reassign it,
        // it changes its priority/queue, so they need to know their claim's status changed.
        await notificationEmitter.NotifyAsync(
            updated.Scope,
            updated.AssignedUserId ?? string.Empty,
            NotificationType.ClaimEscalated,
            "This claim was escalated.",
            updated.CaseId,
            cancellationToken);

        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        return Results.Ok(CaseMapper.ToResponse(updated, auditEvents));
    }

    public static async Task<IResult> ListCases(
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IAuditRepository auditRepository,
        ISlaCalculator slaCalculator,
        INotificationEmitter notificationEmitter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var cases = ClaimAccessGuard.FilterVisibleCases(
            httpContext,
            await caseRepository.ListAsync(scope, cancellationToken));
        var responses = new List<ClaimInboxItemResponse>();

        foreach (var claimCase in cases)
        {
            var documents = await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
            var auditEvents = await auditRepository.ListForCaseAsync(claimCase.CaseId, cancellationToken);
            var lastEvent = auditEvents.OrderByDescending(auditEvent => auditEvent.Timestamp).FirstOrDefault();
            var documentStatus = documents.Count == 0
                ? "New"
                : checklist.MissingRequiredDocuments.Count > 0
                    ? "Missing Documents"
                    : "Ready For Review";
            var slaStatus = slaCalculator.Calculate(claimCase, DateTimeOffset.UtcNow);

            // WO-14: SLA breach has no one-shot write-time event in this codebase — it's a pure,
            // recomputed-on-every-read calculation (BasicSlaCalculator). NotifyOnceAsync makes the
            // first observation of a breach for this case/officer durable without spamming a new
            // notification on every subsequent list load.
            if (slaStatus == SlaStatus.Breached)
            {
                await notificationEmitter.NotifyOnceAsync(
                    claimCase.Scope,
                    claimCase.AssignedUserId ?? string.Empty,
                    NotificationType.SlaBreach,
                    "This claim's SLA has been breached.",
                    claimCase.CaseId,
                    cancellationToken);
            }

            responses.Add(new ClaimInboxItemResponse(
                claimCase.CaseId,
                claimCase.CustomerName,
                claimCase.PolicyNumber,
                claimCase.ProductLine,
                claimCase.ClaimType,
                claimCase.Channel,
                claimCase.Status.ToString(),
                claimCase.CompletenessScore,
                documentStatus,
                claimCase.RiskLevel,
                claimCase.Priority.ToString(),
                slaStatus.ToString(),
                claimCase.AssignedUserId,
                claimCase.AssignedTeam,
                claimCase.UpdatedAt,
                new SafeAuditSummary(
                    auditEvents.Count,
                    lastEvent?.ActionName,
                    lastEvent?.Timestamp,
                    lastEvent?.CorrelationId),
                CaseActions.Allowed(claimCase),
                CaseActions.Blocked(claimCase)));
        }

        return Results.Ok(responses);
    }

    private static async Task<ClaimCase?> GetScopedCaseAsync(
        Guid caseId,
        ICaseRepository caseRepository,
        TenantScope scope,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return null;
        }

        return ClaimAccessGuard.CanViewCase(httpContext, claimCase)
            ? claimCase
            : null;
    }

    private static bool TryResolveScope(
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

        if (TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out scope, out validationError))
        {
            userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
            return true;
        }

        userId = string.Empty;
        return false;
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
