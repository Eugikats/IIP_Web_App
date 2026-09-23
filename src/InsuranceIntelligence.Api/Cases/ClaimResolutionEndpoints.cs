using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Cases;

/// <summary>Outcome O: "when a claim closes, ask one satisfaction question." No path in this
/// repository ever wrote CaseStatus.Settled or Closed before this (ExecuteApprovedNextStep only
/// ever reaches ReadyForExport, the hand-off point to the insurer's own settlement/payment
/// system); this is the missing terminal step, an authorized human confirming what happened after
/// that hand-off, mirroring EscalateCase/ExecuteApprovedNextStep's exact shape.</summary>
public static class ClaimResolutionEndpoints
{
    private static readonly HashSet<CaseStatus> ResolvableFrom = [CaseStatus.ReadyForExport, CaseStatus.Escalated];

    public static async Task<IResult> Resolve(
        Guid caseId,
        ResolveClaimRequest request,
        ICaseRepository caseRepository,
        ICommunicationTemplateRepository templateRepository,
        ICustomerUpdateRepository updateRepository,
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

        if (!Enum.TryParse<CaseStatus>(request.Resolution, ignoreCase: true, out var resolution) || resolution is not (CaseStatus.Settled or CaseStatus.Closed))
        {
            return WorkflowEndpoints.Validation("resolution", "Resolution must be 'Settled' or 'Closed'.");
        }

        if (!ResolvableFrom.Contains(claimCase.Status))
        {
            return Results.Conflict(new
            {
                message = "A claim can only be recorded as settled or closed once it has reached ReadyForExport, or from Escalated for an exception-handled claim."
            });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to record a claim resolution.");
        }

        try
        {
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
                requiresHumanDecision: true,
                ApprovalDecisionGuard.ResolveActorType(actor),
                requestedActionType: "resolveClaim");
            ClaimAuthorityPolicy.EnsureActorMayPerform(ClaimConsequentialAction.ApproveSettlement, ApprovalDecisionGuard.ResolveActorType(actor));
        }
        catch (DomainException exception)
        {
            return WorkflowEndpoints.HumanDecisionRequired(exception.Message);
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return WorkflowEndpoints.Validation("auditReason", "An audit reason is required to record a claim resolution.");
        }

        var now = DateTimeOffset.UtcNow;
        var beforeCase = claimCase.ToAuditSnapshot();
        var updatedCase = claimCase with { Status = resolution, UpdatedAt = now };
        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);

        await caseRepository.SaveAsync(updatedCase, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseStatusUpdated(beforeCase, updatedCase.ToAuditSnapshot(), actor, correlationId, auditReason),
            cancellationToken);

        // Best-effort: a failure to draft the satisfaction question must never undo, or even be
        // reported as failing, a resolution that has already been recorded (same shape as the
        // missing-document-request side effect in WorkflowEndpoints.cs).
        try
        {
            await DraftSatisfactionQuestionAsync(updatedCase, actor, correlationId, templateRepository, updateRepository, auditRepository, cancellationToken);
        }
        catch (Exception)
        {
            // The claim resolution itself already succeeded and is not rolled back; an officer can
            // still draft the satisfaction question manually from a template if this silently
            // failed to auto-draft.
        }

        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        return Results.Ok(CaseMapper.ToResponse(updatedCase, auditEvents));
    }

    /// <summary>Idempotent, same guarantee as CommunicationTemplateEndpoints.SeedDefaults: a
    /// tenant that has customized this template keeps their edit, one that has never seen it gets
    /// the default created on first use rather than requiring a separate manual seeding step.</summary>
    private static async Task DraftSatisfactionQuestionAsync(
        ClaimCase claimCase,
        string actor,
        string correlationId,
        ICommunicationTemplateRepository templateRepository,
        ICustomerUpdateRepository updateRepository,
        IAuditRepository auditRepository,
        CancellationToken cancellationToken)
    {
        const string templateKey = "satisfaction-question-portal";
        var template = await templateRepository.GetCurrentAsync(claimCase.Scope, templateKey, cancellationToken);
        if (template is null)
        {
            var defaultTemplate = DefaultCommunicationTemplates.Defaults().First(t => t.TemplateKey == templateKey);
            var now = DateTimeOffset.UtcNow;
            template = new CommunicationTemplate
            {
                TemplateId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                Scope = claimCase.Scope,
                Version = 1,
                TemplateKey = defaultTemplate.TemplateKey,
                Name = defaultTemplate.Name,
                ReasonCategory = defaultTemplate.ReasonCategory,
                Channel = defaultTemplate.Channel,
                Subject = defaultTemplate.Subject,
                Body = defaultTemplate.Body,
                ShortBody = defaultTemplate.ShortBody,
                Active = true,
                UpdatedByUserId = "system",
                UpdatedAt = now,
                ChangeReason = "Auto-created default satisfaction question template on first claim resolution."
            };
            await templateRepository.SaveAsync(template, cancellationToken);
        }

        var variables = new CommunicationTemplateVariables(claimCase.CustomerName, claimCase.PolicyNumber, string.Empty, claimCase.AssignedUserId);
        var messageBody = CommunicationTemplateRenderer.Render(template.Body, variables);
        var shortMessageBody = CommunicationTemplateRenderer.Render(template.ShortBody, variables);
        var draftedAt = DateTimeOffset.UtcNow;

        var draft = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            ReasonCategory = CustomerUpdateReasonCategory.SatisfactionQuestion,
            Channel = template.Channel,
            Language = template.Language,
            Status = CustomerUpdateStatus.PendingApproval,
            MessageBody = messageBody,
            ShortMessageBody = shortMessageBody,
            CreationMethod = CustomerUpdateCreationMethod.Template,
            SourceTemplateKey = template.TemplateKey,
            RequiresApproval = true,
            Sensitive = false,
            CreatedByUserId = actor,
            CreatedAt = draftedAt,
            UpdatedAt = draftedAt,
            CorrelationId = correlationId
        };

        await updateRepository.SaveAsync(draft, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CustomerUpdateCreatedFromTemplate(draft), cancellationToken);
    }
}

public sealed record ResolveClaimRequest(string Resolution);
