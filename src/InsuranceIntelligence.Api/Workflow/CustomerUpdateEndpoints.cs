using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Workflow;

public static class CustomerUpdateEndpoints
{
    public static async Task<IResult> DraftCustomerUpdate(
        Guid caseId,
        DraftCustomerUpdateRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        ICustomerUpdateDraftingProvider draftingProvider,
        ICustomerUpdateRepository updateRepository,
        IAgentActionLogRepository actionLogRepository,
        IAuditRepository auditRepository,
        IAgentGate agentGate,
        IAgentRunRecorder agentRunRecorder,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        if (!CaseActions.CanDraftCustomerUpdate(claimCase, out var blockedReason))
        {
            var blockedAction = CaseActions.Blocked(claimCase).First(action => action.Action == CaseActions.DraftCustomerUpdate);
            return Results.Conflict(new
            {
                message = blockedReason,
                blockedAction
            });
        }

        if (!Enum.TryParse<CustomerUpdateReasonCategory>(request.ReasonCategory ?? "StatusUpdate", true, out var reasonCategory))
        {
            return WorkflowEndpoints.Validation("reasonCategory", "A supported customer update reason category is required.");
        }

        if (!Enum.TryParse<CommunicationChannel>(request.Channel ?? "Portal", true, out var channel))
        {
            return WorkflowEndpoints.Validation("channel", "A supported communication channel is required.");
        }

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to draft a customer update.");
        }

        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);

        // WO-06: fail closed if a workspace administrator disabled the customer-update-drafting
        // agent, before the drafting provider (and therefore any model) runs.
        var gate = await agentGate.CheckAsync("customer-update-drafting", cancellationToken);
        if (!gate.Enabled)
        {
            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(),
                    scope,
                    caseId,
                    DocumentId: null,
                    UserId: actor,
                    ActorType.User,
                    "agent.killswitch.refused",
                    DateTimeOffset.UtcNow,
                    BeforeStatus: null,
                    AfterStatus: "refused",
                    EvidenceIds: ["customer-update-drafting"],
                    ModelProvider: "AgentGate",
                    Confidence: null,
                    ValidationResult: "refused",
                    ApprovalId: null,
                    AuditReason: gate.RefusalMessage,
                    CorrelationId: correlationId),
                cancellationToken);
            await agentRunRecorder.RecordAsync(
                scope,
                "customer-update-drafting",
                "customer_update_draft",
                [$"{reasonCategory} update for case {caseId}"],
                InsuranceIntelligence.Domain.Agents.AgentRunStatus.Refused,
                startedAt,
                correlationId,
                actor,
                caseId,
                "agent_disabled",
                cancellationToken: cancellationToken);

            return Results.Json(
                new
                {
                    status = "AgentDisabled",
                    agentKey = "customer-update-drafting",
                    message = gate.RefusalMessage
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        CustomerUpdateDraft update;
        try
        {
            update = await draftingProvider.DraftAsync(claimCase, checklist, reasonCategory, channel, actor, correlationId, cancellationToken);
        }
        catch (ProviderNotConfiguredException)
        {
            return Results.Json(
                new
                {
                    status = "NotConfigured",
                    provider = "CustomerUpdateDrafting",
                    message = "Customer update drafting is not configured."
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await updateRepository.SaveAsync(update, cancellationToken);
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            UserId = actor,
            AgentName = "CustomerUpdateAgent",
            AssistantMode = AssistantMode.CustomerUpdateDrafting,
            InputSummary = $"{reasonCategory} update for case {caseId}",
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = update.MessageBody,
            ConfidenceScore = AiConfidenceScore.Create(update.RequiresApproval ? 0.72m : 0.86m),
            RiskFlag = update.RequiresApproval ? "needs_review" : "customer_safe",
            HumanOverride = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId
        };
        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CustomerUpdateDrafted(update), cancellationToken);
        await agentRunRecorder.RecordAsync(
            scope,
            "customer-update-drafting",
            "customer_update_draft",
            [$"{reasonCategory} update for case {caseId}"],
            InsuranceIntelligence.Domain.Agents.AgentRunStatus.Completed,
            startedAt,
            correlationId,
            actor,
            caseId,
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/customer-updates/{update.UpdateId}", WorkflowMapper.ToResponse(update));
    }

    /// <summary>Writes a customer message from scratch, no model involved (outcome D). Goes
    /// through the exact same approval rule as an AI draft, a human-written message about a
    /// sensitive reason category still needs a second person's approval before it can send.</summary>
    public static async Task<IResult> WriteCustomerUpdate(
        Guid caseId,
        WriteCustomerUpdateRequest request,
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        IWorkflowRuleProvider workflowRuleProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            return WorkflowEndpoints.Validation("messageBody", "Message text is required.");
        }

        if (!Enum.TryParse<CustomerUpdateReasonCategory>(request.ReasonCategory ?? "StatusUpdate", true, out var reasonCategory))
        {
            return WorkflowEndpoints.Validation("reasonCategory", "A supported customer update reason category is required.");
        }

        if (!Enum.TryParse<CommunicationChannel>(request.Channel ?? "Portal", true, out var channel))
        {
            return WorkflowEndpoints.Validation("channel", "A supported communication channel is required.");
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to write a customer update.");
        }

        var messageBody = request.MessageBody.Trim();
        var shortMessageBody = string.IsNullOrWhiteSpace(request.ShortMessageBody) ? Truncate(messageBody, 160) : request.ShortMessageBody.Trim();
        var sensitive = workflowRuleProvider.CustomerUpdateRequiresApproval(messageBody);
        var requiresApproval = CustomerUpdateApprovalPolicy.RequiresApproval(sensitive, reasonCategory);
        var now = DateTimeOffset.UtcNow;
        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);

        var update = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ReasonCategory = reasonCategory,
            Channel = channel,
            Status = requiresApproval ? CustomerUpdateStatus.PendingApproval : CustomerUpdateStatus.Draft,
            MessageBody = messageBody,
            ShortMessageBody = shortMessageBody,
            CreationMethod = CustomerUpdateCreationMethod.ManuallyWritten,
            RequiresApproval = requiresApproval,
            Sensitive = sensitive,
            CreatedByUserId = actor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };

        await updateRepository.SaveAsync(update, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CustomerUpdateWritten(update), cancellationToken);

        return Results.Created($"/api/v1/customer-updates/{update.UpdateId}", WorkflowMapper.ToResponse(update));
    }

    /// <summary>Sends off a configured template, filled with this claim's own facts (outcome D).
    /// Creates an ordinary CustomerUpdateDraft, obeying the exact same approval rule as any other
    /// message, a template is a starting point, not a way around review.</summary>
    public static async Task<IResult> CreateCustomerUpdateFromTemplate(
        Guid caseId,
        CreateCustomerUpdateFromTemplateRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        ICommunicationTemplateRepository templateRepository,
        ICustomerUpdateRepository updateRepository,
        IWorkflowRuleProvider workflowRuleProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        if (string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            return WorkflowEndpoints.Validation("templateKey", "A template is required.");
        }

        var template = await templateRepository.GetCurrentAsync(scope, request.TemplateKey.Trim(), cancellationToken);
        if (template is null || !template.Active)
        {
            return Results.NotFound(new { message = "That template was not found or is not active for the current tenant workspace." });
        }

        var channel = Enum.TryParse<CommunicationChannel>(request.Channel, true, out var requestedChannel) ? requestedChannel : template.Channel;

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var missingDocuments = checklist.MissingRequiredDocuments.Count == 0
            ? "the required supporting documents"
            : string.Join(", ", checklist.MissingRequiredDocuments.Select(document => document.Label));
        var variables = new CommunicationTemplateVariables(
            claimCase.CustomerName,
            claimCase.PolicyNumber,
            missingDocuments,
            claimCase.AssignedUserId);

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to send a template.");
        }

        var messageBody = CommunicationTemplateRenderer.Render(template.Body, variables);
        var shortMessageBody = CommunicationTemplateRenderer.Render(template.ShortBody, variables);
        var sensitive = workflowRuleProvider.CustomerUpdateRequiresApproval(messageBody);
        var requiresApproval = CustomerUpdateApprovalPolicy.RequiresApproval(sensitive, template.ReasonCategory);
        var now = DateTimeOffset.UtcNow;
        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);

        var update = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ReasonCategory = template.ReasonCategory,
            Channel = channel,
            Language = template.Language,
            Status = requiresApproval ? CustomerUpdateStatus.PendingApproval : CustomerUpdateStatus.Draft,
            MessageBody = messageBody,
            ShortMessageBody = shortMessageBody,
            CreationMethod = CustomerUpdateCreationMethod.Template,
            SourceTemplateKey = template.TemplateKey,
            RequiresApproval = requiresApproval,
            Sensitive = sensitive,
            CreatedByUserId = actor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };

        await updateRepository.SaveAsync(update, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CustomerUpdateCreatedFromTemplate(update), cancellationToken);

        return Results.Created($"/api/v1/customer-updates/{update.UpdateId}", WorkflowMapper.ToResponse(update));
    }

    /// <summary>Edits a drafted message before it is sent (outcome D). The model's (or the
    /// previous edit's) original text is always kept in OriginalMessageBody, and who changed it and
    /// when is always recorded. Editing an Approved draft returns it to PendingApproval unless the
    /// editor approves in the same step, which is recorded as exactly that, never as a silent
    /// override. A sent message can never be edited, a correction is a new message.</summary>
    public static async Task<IResult> EditCustomerUpdate(
        Guid updateId,
        EditCustomerUpdateRequest request,
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var update = await updateRepository.GetAsync(updateId, scope, cancellationToken);
        if (update is null)
        {
            return Results.NotFound(new { message = "Customer update was not found for the requested tenant workspace." });
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, update.CaseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        if (update.Status is CustomerUpdateStatus.Sent or CustomerUpdateStatus.SimulatedSent)
        {
            return Results.Conflict(new { message = "A sent message can never be edited, a correction is a new message." });
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            return WorkflowEndpoints.Validation("messageBody", "Message text is required.");
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to edit a customer update.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return WorkflowEndpoints.Validation("auditReason", "A reason is required to edit a customer update.");
        }

        var messageBody = request.MessageBody.Trim();
        var shortMessageBody = string.IsNullOrWhiteSpace(request.ShortMessageBody) ? Truncate(messageBody, 160) : request.ShortMessageBody.Trim();
        var wasApproved = update.Status == CustomerUpdateStatus.Approved;
        var approveInSameStep = wasApproved && request.ApproveInSameStep;
        var now = DateTimeOffset.UtcNow;

        var edited = update with
        {
            MessageBody = messageBody,
            ShortMessageBody = shortMessageBody,
            // The very first text this message ever had, kept once, on the first edit only.
            OriginalMessageBody = update.OriginalMessageBody ?? (update.CreationMethod == CustomerUpdateCreationMethod.AiDrafted ? update.MessageBody : null),
            EditedByUserId = actor,
            EditedAt = now,
            Status = wasApproved && !approveInSameStep ? CustomerUpdateStatus.PendingApproval : update.Status,
            ApprovedByUserId = approveInSameStep ? actor : update.ApprovedByUserId,
            ApprovedAt = approveInSameStep ? now : update.ApprovedAt,
            UpdatedAt = now
        };

        await updateRepository.SaveAsync(edited, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CustomerUpdateEdited(edited, actor, approveInSameStep, auditReason), cancellationToken);

        return Results.Ok(WorkflowMapper.ToResponse(edited));
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";

    // WO-16: tenant-wide view backing Communications.tsx, the previous ListCustomerUpdates below
    // is per-case only. Each row is filtered through the same ClaimAccessGuard that gates every
    // other cross-case read (e.g. CommunicationEndpoints.ListHistory), so an officer scoped to only
    // their assigned claims never sees another officer's customer updates.
    public static async Task<IResult> ListAllCustomerUpdates(
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var updates = await updateRepository.ListAsync(scope, cancellationToken);
        var responses = new List<CustomerUpdateWithCaseResponse>();
        foreach (var update in updates)
        {
            var claimCase = await caseRepository.GetAsync(update.CaseId, cancellationToken);
            if (claimCase is null || !claimCase.Scope.Matches(scope) || !ClaimAccessGuard.CanViewCase(httpContext, claimCase))
            {
                continue;
            }

            responses.Add(new CustomerUpdateWithCaseResponse(
                WorkflowMapper.ToResponse(update),
                claimCase.CustomerName,
                claimCase.CustomerEmail,
                claimCase.CustomerPhone,
                claimCase.PolicyNumber));
        }

        return Results.Ok(responses.OrderByDescending(item => item.Update.UpdatedAt).ToArray());
    }

    public static async Task<IResult> ListCustomerUpdates(
        Guid caseId,
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        var updates = await updateRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(updates.Select(WorkflowMapper.ToResponse).ToArray());
    }

    public static Task<IResult> ApproveCustomerUpdate(
        Guid updateId,
        CustomerUpdateDecisionRequest request,
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        DecideAsync(updateId, request, CustomerUpdateStatus.Approved, caseRepository, updateRepository, auditRepository, httpContext, cancellationToken);

    public static Task<IResult> RejectCustomerUpdate(
        Guid updateId,
        CustomerUpdateDecisionRequest request,
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        DecideAsync(updateId, request, CustomerUpdateStatus.Rejected, caseRepository, updateRepository, auditRepository, httpContext, cancellationToken);

    public static async Task<IResult> SendCustomerUpdate(
        Guid updateId,
        CustomerUpdateDecisionRequest request,
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        ICommunicationProvider communicationProvider,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var update = await updateRepository.GetAsync(updateId, scope, cancellationToken);
        if (update is null)
        {
            return Results.NotFound(new { message = "Customer update was not found for the requested tenant workspace." });
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, update.CaseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        var sendAction = CustomerUpdateActions.Allowed(update).FirstOrDefault(action => action.Action == "sendCustomerUpdate");
        if (sendAction is null)
        {
            var blockedAction = CustomerUpdateActions.Blocked(update).FirstOrDefault(action => action.Action == "sendCustomerUpdate");
            return Results.Conflict(new
            {
                message = blockedAction?.Reason ?? "This customer update is not currently sendable.",
                blockedAction
            });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to send a customer update.");
        }

        try
        {
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
                requiresHumanDecision: true,
                ApprovalDecisionGuard.ResolveActorType(actor),
                requestedActionType: "sendCustomerUpdate");
        }
        catch (DomainException exception)
        {
            return WorkflowEndpoints.HumanDecisionRequired(exception.Message);
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return WorkflowEndpoints.Validation("auditReason", "An audit reason is required to send a customer update.");
        }

        var sent = await communicationProvider.SendAsync(update, actor, request.Recipient, cancellationToken);
        if (sent.DeliveryStatus is "ApprovalRequired" or "ConfigurationRequired" or "Failed")
        {
            return Results.BadRequest(new
            {
                deliveryStatus = sent.DeliveryStatus,
                message = sent.DeliveryStatus == "ConfigurationRequired"
                    ? "Customer update sending is not configured for this channel or recipient."
                    : "Customer update could not be sent."
            });
        }

        await updateRepository.SaveAsync(sent, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CustomerUpdateSent(sent, actor, auditReason),
            cancellationToken);

        return Results.Ok(WorkflowMapper.ToResponse(sent));
    }

    private static async Task<IResult> DecideAsync(
        Guid updateId,
        CustomerUpdateDecisionRequest request,
        CustomerUpdateStatus status,
        ICaseRepository caseRepository,
        ICustomerUpdateRepository updateRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var update = await updateRepository.GetAsync(updateId, scope, cancellationToken);
        if (update is null)
        {
            return Results.NotFound(new { message = "Customer update was not found for the requested tenant workspace." });
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, update.CaseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        var actionName = status == CustomerUpdateStatus.Approved
            ? "approveCustomerUpdate"
            : "rejectCustomerUpdate";
        var allowedAction = CustomerUpdateActions.Allowed(update).FirstOrDefault(action => action.Action == actionName);
        if (allowedAction is null)
        {
            var blockedAction = CustomerUpdateActions.Blocked(update).FirstOrDefault(action => action.Action == actionName);
            return Results.Conflict(new
            {
                message = blockedAction?.Reason ?? "This customer update decision is not currently allowed.",
                blockedAction
            });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to review a customer update.");
        }

        try
        {
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
                requiresHumanDecision: true,
                ApprovalDecisionGuard.ResolveActorType(actor),
                requestedActionType: actionName);
        }
        catch (DomainException exception)
        {
            return WorkflowEndpoints.HumanDecisionRequired(exception.Message);
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return WorkflowEndpoints.Validation("auditReason", "An audit reason is required to approve or reject a customer update.");
        }

        var updated = update with
        {
            Status = status,
            ApprovedByUserId = status == CustomerUpdateStatus.Approved ? actor : update.ApprovedByUserId,
            ApprovedAt = status == CustomerUpdateStatus.Approved ? DateTimeOffset.UtcNow : update.ApprovedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await updateRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            status == CustomerUpdateStatus.Approved
                ? AuditEvent.CustomerUpdateApproved(updated, actor, auditReason)
                : AuditEvent.CustomerUpdateRejected(updated, actor, auditReason),
            cancellationToken);

        return Results.Ok(WorkflowMapper.ToResponse(updated));
    }
}
