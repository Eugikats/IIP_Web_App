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
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Api.Cases;

public static class CaseEndpoints
{
    public static async Task<IResult> CreateCase(
        CreateCaseRequest request,
        CaseIntakeService service,
        IAuditRepository auditRepository,
        InsuranceIntelligence.Application.Cases.IFnolAcknowledgementService acknowledgementService,
        IDocumentChecklistProvider checklistProvider,
        ILogger<CaseFnolAcknowledgementLogMarker> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        InsuranceIntelligence.Api.Agents.IClaimTriageQueue? triageQueue = null,
        INotificationEmitter? notificationEmitter = null)
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
                request.MedicalFnolDetails,
                request.ClaimedAmount,
                RequireConfiguredClaimType: true,
                FieldValues: request.FieldValues);

            var claimCase = await service.CreateCaseAsync(command, cancellationToken);

            // A failed or blocked acknowledgement must never fail claim creation, which already
            // happened above, see IFnolAcknowledgementService's own doc comment.
            try
            {
                await acknowledgementService.SendAsync(claimCase, correlationId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FNOL acknowledgement failed for case {CaseId}", claimCase.CaseId);
            }

            // Outcome E: the officer a claim lands on at intake needs to hear about it, not
            // discover it by refreshing their queue. A failed notification must never fail claim
            // creation, which has already happened above.
            if (notificationEmitter is not null && !string.IsNullOrWhiteSpace(claimCase.AssignedUserId))
            {
                try
                {
                    await notificationEmitter.NotifyAsync(
                        claimCase.Scope,
                        claimCase.AssignedUserId,
                        NotificationType.ClaimAssigned,
                        $"New {claimCase.ClaimType} claim assigned to you on the {claimCase.AssignedTeam} team.",
                        claimCase.CaseId,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Assignment notification failed for case {CaseId}", claimCase.CaseId);
                }
            }

            // The claims agent reviews every new claim straight away, in the background, so the
            // review is waiting when the handler opens the claim. Never blocks the response.
            if (triageQueue is not null)
            {
                await triageQueue.EnqueueAsync(claimCase.Scope, claimCase.CaseId, correlationId, "staff_intake", cancellationToken);
            }

            var auditEvents = await auditRepository.ListForCaseAsync(claimCase.CaseId, cancellationToken);

            // WO 5.3: the completeness check runs right here, at submission, no documents exist
            // yet, so every RequiredDocuments entry starts out Missing by definition. That is the
            // point: the submitter sees the full checklist immediately, not only after extraction
            // runs on whatever gets uploaded later.
            var checklist = await checklistProvider.BuildChecklistAsync(claimCase, [], cancellationToken);
            var response = CaseMapper.ToResponse(claimCase, auditEvents, checklist);

            return Results.Created($"/api/v1/cases/{claimCase.CaseId}", response);
        }
        catch (ClaimFieldsInvalidException ex)
        {
            return ClaimFieldEndpoints.FieldErrors(ex.Errors);
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

    /// <summary>The claim's progress (outcome H): the pinned product's own configured claim
    /// process (outcome A), never a fabricated or claim-type-hardcoded stage list. Honestly
    /// "not configured" when the claim has no pinned product, exactly like any other absent
    /// capability. A separate, lightly-permissioned endpoint (claim-view, not ManageProducts)
    /// rather than folding this into CaseResponse, so every claim list read does not pay for a
    /// product lookup it does not need.</summary>
    public static async Task<IResult> GetProductStages(
        Guid caseId,
        ICaseRepository caseRepository,
        InsuranceIntelligence.Domain.Products.IProductRepository productRepository,
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

        var claimCase = await GetScopedCaseAsync(caseId, caseRepository, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        if (claimCase.ProductId is not { } productId || claimCase.ProductVersion is not { } version)
        {
            return Results.Ok(new ClaimProcessResponse(false, null, null, []));
        }

        var product = await productRepository.GetVersionAsync(scope, productId, version, cancellationToken);
        if (product is null)
        {
            return Results.Ok(new ClaimProcessResponse(false, null, null, []));
        }

        var stages = product.Stages
            .OrderBy(stage => stage.SortOrder)
            .Select(stage => new ClaimProcessStageResponse(stage.StageKey, stage.Name, stage.OwnerRole, stage.IsAutomatic, stage.SortOrder, stage.ServiceStandardWorkingDays))
            .ToArray();

        return Results.Ok(new ClaimProcessResponse(true, product.Name, product.Version, stages));
    }

    public static async Task<IResult> AssignCase(
        Guid caseId,
        AssignCaseRequest request,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        INotificationEmitter? notificationEmitter = null)
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
        await ClaimStatusNotifications.NotifyAsync(httpContext, claimCase, updated, correlationId, cancellationToken);

        if (notificationEmitter is not null
            && !string.IsNullOrWhiteSpace(updated.AssignedUserId)
            && !string.Equals(updated.AssignedUserId, claimCase.AssignedUserId, StringComparison.Ordinal))
        {
            await notificationEmitter.NotifyAsync(
                updated.Scope,
                updated.AssignedUserId,
                NotificationType.ClaimAssigned,
                $"{claimCase.ClaimType} claim reassigned to you by {userId}.",
                updated.CaseId,
                cancellationToken);
        }

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
        await ClaimStatusNotifications.NotifyAsync(httpContext, claimCase, updated, correlationId, cancellationToken);

        // Notify the officer already assigned to this claim, escalation doesn't reassign it,
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

    /// <summary>
    /// A human confirming a claim IIP auto-created from an identified WhatsApp/email sender (see
    /// RuleBasedWhatsAppToCaseMatcher / RuleBasedEmailToCaseMatcher). This is what starts the real
    /// SLA clock and removes it from the "needs confirmation" state, a claim IIP created on its
    /// own never counts toward SLA or attention metrics until a human has looked at it.
    /// </summary>
    public static async Task<IResult> ConfirmCase(
        Guid caseId,
        ConfirmCaseRequest request,
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
                ["userId"] = ["Authenticated user context is required to confirm a claim."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.AuditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["auditReason"] = ["A reason is required to confirm a claim IIP created automatically."]
            });
        }

        ClaimCaseConfirmationResult result;
        try
        {
            result = await service.ConfirmCaseAsync(
                caseId,
                scope,
                new ConfirmCaseCorrections(request.ProductLine, request.ClaimType, request.IncidentDate, request.IncidentDescription),
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            });
        }

        if (!result.Found)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        if (result.WasAlreadyConfirmed)
        {
            return Results.Conflict(new { message = "This claim has already been confirmed.", blockedAction = "confirmCase" });
        }

        var correlationId = GetCorrelationId(httpContext);
        await auditRepository.WriteAsync(
            AuditEvent.CaseConfirmed(result.Before!.ToAuditSnapshot(), result.After!.ToAuditSnapshot(), userId, correlationId, request.AuditReason.Trim()),
            cancellationToken);

        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        return Results.Ok(CaseMapper.ToResponse(result.After!, auditEvents));
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

        // Two reads for the whole list, not two per case: with 50 cases that is the difference between 3 queries and 100.
        var caseIds = cases.Select(claimCase => claimCase.CaseId).ToArray();
        var documentsByCase = await documentRepository.ListForCasesAsync(caseIds, scope, cancellationToken);
        var auditByCase = await auditRepository.ListForCasesAsync(caseIds, cancellationToken);

        foreach (var claimCase in cases)
        {
            var documents = documentsByCase.TryGetValue(claimCase.CaseId, out var caseDocuments) ? caseDocuments : [];
            var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
            var auditEvents = auditByCase.TryGetValue(claimCase.CaseId, out var caseAudit) ? caseAudit : [];
            var lastEvent = auditEvents.OrderByDescending(auditEvent => auditEvent.Timestamp).FirstOrDefault();
            var documentStatus = documents.Count == 0
                ? "New"
                : checklist.MissingRequiredDocuments.Count > 0
                    ? "Missing Documents"
                    : "Ready For Review";
            var slaStatus = slaCalculator.Calculate(claimCase, DateTimeOffset.UtcNow);

            // WO-14: SLA breach has no one-shot write-time event in this codebase, it's a pure,
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
                claimCase.RequiresConfirmation,
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

/// <summary>Empty marker type purely so ILogger&lt;T&gt; has a stable category name for this
/// static-class endpoint (ILogger&lt;CaseEndpoints&gt; is not usable, the class is static),
/// matching SmsIntegrationEndpoints' SmsDeliveryStatusLogMarker precedent.</summary>
public sealed class CaseFnolAcknowledgementLogMarker;
