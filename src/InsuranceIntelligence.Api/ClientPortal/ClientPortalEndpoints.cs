using InsuranceIntelligence.Api.Assistant;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http.Features;

namespace InsuranceIntelligence.Api.ClientPortal;

public static class ClientPortalEndpoints
{
    public static async Task<IResult> GetClaim(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        ICommunicationRepository communicationRepository,
        IClaimReviewRequestRepository reviewRequestRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IAuditRepository? auditRepository = null,
        ISlotPdfRepository? slotPdfRepository = null)
    {
        if (!ResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var communications = await communicationRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var reviewRequests = await reviewRequestRepository.ListForCaseAsync(caseId, scope, cancellationToken);

        await LogDisclosureAsync(auditRepository, httpContext, scope, caseId, "ClaimStatus", cancellationToken);

        return Results.Ok(await WithPdfsAsync(ToResponse(claimCase, checklist, documents, communications, reviewRequests), slotPdfRepository, scope, caseId, cancellationToken));
    }

    /// <summary>Outcome L: "the customer sees their policy" -- cover dates, cover status (computed
    /// deterministically from those dates), and coverage lines. Premium status is deliberately
    /// reported as NotConfigured rather than guessed: no billing/payment-status subsystem exists
    /// anywhere in this codebase to answer paid/due/lapsed honestly.</summary>
    public static async Task<IResult> GetPolicy(
        IPolicyRepository policyRepository,
        ICoverageRepository coverageRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IAuditRepository? auditRepository = null)
    {
        if (httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.SessionItemKey, out var stashed) is false
            || stashed is not ClaimantPortalSession session)
        {
            return Results.Unauthorized();
        }

        var policy = await policyRepository.GetByPolicyNumberAsync(session.PolicyNumber, session.Scope, cancellationToken);
        if (policy is null)
        {
            return Results.NotFound(new { message = "The policy on this access link could no longer be verified." });
        }

        var coverages = await coverageRepository.ListForPolicyAsync(policy.PolicyId, session.Scope, cancellationToken);

        await LogDisclosureAsync(auditRepository, httpContext, session.Scope, session.CaseId, "Policy", cancellationToken, session);

        return Results.Ok(ToPolicyResponse(policy, coverages));
    }

    /// <summary>Outcome L: "ask a question about the wording and get a cited answer." Reuses the
    /// exact same retrieval/answer engine AssistantEndpoints.AskPolicyQuestion uses for staff, with
    /// a claimant session in place of a staff permission: product line is resolved from the
    /// session's own verified policy, never taken from the caller, and the audience is always
    /// Customer, so this can never be used to probe grounded answers for a product the claimant
    /// does not hold.</summary>
    public static async Task<IResult> AskPolicyQuestion(
        ClientPortalPolicyQuestionRequest request,
        IPolicyRepository policyRepository,
        IRetrievalProvider retrievalProvider,
        IAiAssistantProvider assistantProvider,
        IAgentGate agentGate,
        IAgentActionLogRepository actionLogRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IAuditRepository? auditRepository = null)
    {
        if (httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.SessionItemKey, out var stashed) is false
            || stashed is not ClaimantPortalSession session)
        {
            return Results.Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Question))
        {
            return Validation("question", "A question is required.");
        }

        var policy = await policyRepository.GetByPolicyNumberAsync(session.PolicyNumber, session.Scope, cancellationToken);
        var correlationId = GetCorrelationId(httpContext);
        var question = new AssistantQuestion(
            session.Scope,
            session.CaseId,
            request.Question.Trim(),
            policy?.ProductLine,
            "Customer",
            session.ContactValue,
            correlationId);

        var gate = await agentGate.CheckAsync(session.Scope, "policy-intelligence", cancellationToken);
        var answer = gate.Enabled
            ? await AnswerQuestionAsync(retrievalProvider, assistantProvider, question, cancellationToken)
            : new AssistantAnswer(
                gate.RefusalMessage,
                AiConfidenceScore.Create(0m),
                EscalationRequired: true,
                Array.Empty<SourceCitation>(),
                gate.RefusalMessage,
                "agent_disabled",
                "AgentDisabled",
                null,
                "not-configured",
                "CustomerSafe");

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = session.Scope,
            CaseId = question.CaseId,
            UserId = question.UserId,
            AgentName = "PolicyQnAAgent",
            AssistantMode = AssistantMode.PolicyQnA,
            InputSummary = SummarizeInput(question.Question),
            RetrievedSources = answer.Sources,
            ModelOutput = answer.AnswerText,
            ConfidenceScore = answer.ConfidenceScore,
            RiskFlag = answer.RiskFlag,
            HumanOverride = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            ModelProvider = answer.ModelProvider,
            ModelDeployment = answer.ModelDeployment,
            PromptVersion = answer.PromptVersion,
            Visibility = answer.Visibility,
            CanTriggerDecision = answer.CanTriggerDecision,
            ModelVersion = answer.ModelVersion,
            PromptTokens = answer.PromptTokens,
            CompletionTokens = answer.CompletionTokens,
            EstimatedCostUsd = AiCostEstimator.Estimate(answer.PromptTokens, answer.CompletionTokens)
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        if (auditRepository is not null)
        {
            await auditRepository.WriteAsync(AuditEvent.AssistantPolicyQuestionAnswered(actionLog), cancellationToken);
        }

        await LogDisclosureAsync(auditRepository, httpContext, session.Scope, session.CaseId, "PolicyQuestion", cancellationToken, session);

        return Results.Ok(new ClientPortalPolicyQuestionResponse(
            answer.AnswerText,
            answer.EscalationRequired,
            answer.Sources.Select(AssistantMapper.ToResponse).ToArray()));
    }

    /// <summary>Outcome L: "request a change to their contact details for staff approval." Never
    /// applies the change itself, only proposes it; ContactDetailChangeEndpoints (staff-side) is
    /// where a person actually accepts or rejects it.</summary>
    public static async Task<IResult> RequestContactDetailChange(
        ClientPortalContactDetailChangeRequestBody body,
        IContactDetailChangeRequestRepository changeRequestRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.SessionItemKey, out var stashed) is false
            || stashed is not ClaimantPortalSession session)
        {
            return Results.Unauthorized();
        }

        if (body is null || (string.IsNullOrWhiteSpace(body.Email) && string.IsNullOrWhiteSpace(body.Phone)))
        {
            return Validation("request", "A new email address or phone number is required.");
        }

        var correlationId = GetCorrelationId(httpContext);
        ContactDetailChangeRequest request;
        try
        {
            request = ContactDetailChangeRequest.Create(
                session.Scope, session.PolicyNumber, session.CaseId, session.ContactValue,
                body.Email, body.Phone, body.Note, correlationId);
        }
        catch (ArgumentException ex)
        {
            return Validation("request", ex.Message);
        }

        await changeRequestRepository.SaveAsync(request, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ContactDetailChangeRequested(request), cancellationToken);

        return Results.Ok(new ClientPortalContactDetailChangeResponse(
            request.RequestId,
            request.Status.ToString(),
            request.RequestedAt,
            "Your request to update your contact details has been logged. A member of staff will review and confirm it before it takes effect."));
    }

    public static async Task<IResult> UploadDocuments(
        Guid caseId,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentChecklistProvider checklistProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        ICommunicationRepository communicationRepository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        InsuranceIntelligence.Application.Compliance.IClaimantConsentService consentService,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IImageDerivativeProvider? derivativeProvider = null,
        ISlotPdfService? slotPdfService = null,
        ISlotPdfRepository? slotPdfRepository = null,
        ILoggerFactory? derivativeLoggerFactory = null)
    {
        if (!ResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        // Nothing is accepted from the portal until the claimant has agreed to the privacy notice
        // that is in force (build-loop item 9).
        if (!await consentService.HasConsentAsync(scope, ResolveActor(httpContext), cancellationToken))
        {
            return ClaimantConsentEndpoints.ConsentRequired();
        }

        if (!IsMultipartFormData(httpContext.Request.ContentType))
        {
            return Validation("files", "Document upload must use multipart/form-data.");
        }

        var form = httpContext.Features.Get<IFormFeature>()?.Form
            ?? await httpContext.Request.ReadFormAsync(cancellationToken);
        IReadOnlyList<IFormFile> files = form.Files.GetFiles("files");
        if (files.Count == 0)
        {
            files = form.Files.ToArray();
        }

        if (files.Count == 0)
        {
            return Validation("files", "At least one document file is required.");
        }

        foreach (var file in files)
        {
            var fileError = ValidateUploadFile(file);
            if (fileError is not null)
            {
                return Validation("files", fileError);
            }
        }

        // The customer's own choice of which required document this is (outcome B). Absent for an
        // older client and for the catch-all "something else" slot, in which case the classifier's
        // guess decides, exactly as before.
        var documentTypeKey = NormalizeDocumentTypeKey(form["documentTypeKey"]);
        // "Something else" has no slot: it is treated as paperwork, not as evidence.
        DocumentKind? slotKind = documentTypeKey is null ? null : DocumentKind.Document;
        var nextPageOrder = 1;

        var actor = ResolveActor(httpContext);
        var correlationId = httpContext.TraceIdentifier;

        if (documentTypeKey is not null)
        {
            var existingDocuments = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
            var slot = (await checklistProvider.BuildChecklistAsync(claimCase, existingDocuments, cancellationToken))
                .Slots.FirstOrDefault(candidate => string.Equals(candidate.Key, documentTypeKey, StringComparison.OrdinalIgnoreCase));
            if (slot is null)
            {
                return Validation("documentTypeKey", "That is not one of the documents asked for on this claim.");
            }

            slotKind = slot.Kind;
            nextPageOrder = SlotPdfService.PagesInOrder(existingDocuments, slot.Key).Count + 1;
            var slotError = SlotUploadError(slot, files, existingDocuments);
            if (slotError is not null)
            {
                return Validation("files", slotError);
            }

            // A single-file slot is replaced by a new upload; a multi-file slot (pages of one
            // document, or several photos) keeps what is already there and adds to it.
            if (!slot.AllowsMultipleFiles)
            {
                await SupersedeActiveDocumentsAsync(documentRepository, auditRepository, caseId, scope, documentTypeKey, actor, correlationId, cancellationToken);
            }
        }

        foreach (var file in files)
        {
            var documentId = Guid.NewGuid();
            // The original is stored byte for byte; its hash and a separate preview are kept beside it.
            var stored = await DocumentOriginals.StoreAsync(
                file, documentId, caseId, scope, NormalizeContentType(file), storageProvider, derivativeProvider, cancellationToken, derivativeLoggerFactory);

            var uploadedDocument = CreateUploadedDocument(documentId, claimCase, file, stored.Storage, actor, correlationId, documentTypeKey) with
            {
                Sha256 = stored.Sha256,
                SlotKind = slotKind,
                PreviewObjectKey = stored.PreviewObjectKey,
                PageOrder = slotKind == DocumentKind.Document && documentTypeKey is not null ? nextPageOrder++ : null,
            };
            await documentRepository.SaveAsync(uploadedDocument, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.DocumentUploaded(uploadedDocument, actor, correlationId), cancellationToken);

            // A failure here (provider missing or unreadable file) leaves the document marked
            // Failed with a retry, never stuck at "Being checked"; the customer is not shown a
            // server error for something they cannot fix.
            await DocumentProcessingRunner.RunAsync(
                uploadedDocument,
                claimCase,
                storageProvider,
                processingProvider,
                fieldValidationRuleRepository,
                documentRepository,
                auditRepository,
                correlationId,
                cancellationToken);
        }

        // Pictures of paperwork become pages of one PDF, rebuilt from what the slot holds now.
        if (slotPdfService is not null && slotKind == DocumentKind.Document && documentTypeKey is not null)
        {
            await slotPdfService.RebuildAsync(scope, caseId, documentTypeKey, correlationId, cancellationToken);
        }

        var allDocuments = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, allDocuments, cancellationToken);
        var updatedCase = claimCase with
        {
            CompletenessScore = checklist.CompletenessScore.Percentage,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await caseRepository.SaveAsync(updatedCase, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseCompletenessUpdated(
                claimCase.ToAuditSnapshot(),
                updatedCase.ToAuditSnapshot(),
                updatedCase.CompletenessScore,
                correlationId),
            cancellationToken);

        var communications = await communicationRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(await WithPdfsAsync(ToResponse(updatedCase, checklist, allDocuments, communications), slotPdfRepository, scope, caseId, cancellationToken));
    }

    public static async Task<IResult> RequestReview(
        Guid caseId,
        ClientPortalReviewRequestBody body,
        ICaseRepository caseRepository,
        IClaimReviewRequestRepository reviewRequestRepository,
        IAuditRepository auditRepository,
        INotificationEmitter notificationEmitter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Reason))
        {
            return Validation("reason", "Tell us briefly why you would like a human to re-review this claim.");
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        var actor = ResolveActor(httpContext);
        var correlationId = httpContext.TraceIdentifier;
        var request = ClaimReviewRequest.Create(
            scope,
            caseId,
            ClaimReviewRequestOrigin.Customer,
            actor,
            body.Reason,
            correlationId);

        await reviewRequestRepository.SaveAsync(request, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ClaimReviewRequested(request), cancellationToken);

        await notificationEmitter.NotifyAsync(
            scope,
            claimCase.AssignedUserId ?? string.Empty,
            NotificationType.ReviewRequested,
            "The customer requested a human review of this claim.",
            caseId,
            cancellationToken);

        return Results.Ok(new ClientPortalReviewRequestResponse(
            request.RequestId,
            request.Status.ToString(),
            request.RequestedAt,
            "Your request for a human re-review has been logged. A claims officer will review it. This decision is not final AI, a person will look at it."));
    }

    /// <summary>"Remove", with no replacement file (outcome B). Never deletes anything
    /// (non-negotiable 2): marks the active document(s) for this requirement superseded, so the
    /// requirement goes back to not satisfied and staff still see what was originally sent.</summary>
    /// <summary>The customer taps "Try again" on a file that could not be read. The stored file is
    /// processed again; nothing is uploaded a second time.</summary>
    public static async Task<IResult> RetryDocument(
        Guid caseId,
        ClientPortalRemoveDocumentRequest request,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentChecklistProvider checklistProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        ICommunicationRepository communicationRepository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        ISlotPdfRepository? slotPdfRepository = null)
    {
        if (!ResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        var key = NormalizeDocumentTypeKey(request.DocumentTypeKey);
        if (key is null)
        {
            return Validation("documentTypeKey", "Say which document to try again.");
        }

        var documents = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var failed = documents
            .Where(document => !document.IsSuperseded && document.ProcessingStatus == DocumentProcessingStatus.Failed)
            .Where(document => string.Equals(document.EffectiveKey, key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(document => document.UploadedAt)
            .FirstOrDefault();
        if (failed is null)
        {
            return Results.Conflict(new { message = "There is nothing to try again for that document." });
        }

        var correlationId = httpContext.TraceIdentifier;
        await DocumentProcessingRunner.RunAsync(
            failed with { ProcessingStatus = DocumentProcessingStatus.Uploaded },
            claimCase,
            storageProvider,
            processingProvider,
            fieldValidationRuleRepository,
            documentRepository,
            auditRepository,
            correlationId,
            cancellationToken);

        var allDocuments = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, allDocuments, cancellationToken);
        var updatedCase = claimCase with { CompletenessScore = checklist.CompletenessScore.Percentage, UpdatedAt = DateTimeOffset.UtcNow };
        await caseRepository.SaveAsync(updatedCase, cancellationToken);
        var communications = await communicationRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(await WithPdfsAsync(ToResponse(updatedCase, checklist, allDocuments, communications), slotPdfRepository, scope, caseId, cancellationToken));
    }

    public static async Task<IResult> RemoveDocument(
        Guid caseId,
        ClientPortalRemoveDocumentRequest body,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        ICommunicationRepository communicationRepository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        ISlotPdfService? slotPdfService = null,
        ISlotPdfRepository? slotPdfRepository = null)
    {
        if (!ResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var documentTypeKey = NormalizeDocumentTypeKey(body?.DocumentTypeKey);
        if (documentTypeKey is null)
        {
            return Validation("documentTypeKey", "Which document to remove is required.");
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        var actor = ResolveActor(httpContext);
        var correlationId = httpContext.TraceIdentifier;
        bool supersededAny;
        if (body?.DocumentId is { } pageId)
        {
            // One page of a multi-page slot. The file is kept and marked superseded, like any removal.
            var pages = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
            var page = pages.FirstOrDefault(candidate => candidate.DocumentId == pageId && !candidate.IsSuperseded
                && string.Equals(candidate.EffectiveKey, documentTypeKey, StringComparison.OrdinalIgnoreCase));
            supersededAny = page is not null;
            if (page is not null)
            {
                await documentRepository.SaveAsync(page with { IsSuperseded = true, SupersededAt = DateTimeOffset.UtcNow }, cancellationToken);
                await auditRepository.WriteAsync(AuditEvent.SlotPagesChanged(scope, caseId, documentTypeKey, "document.page.removed", actor, correlationId), cancellationToken);
            }
        }
        else
        {
            supersededAny = await SupersedeActiveDocumentsAsync(documentRepository, auditRepository, caseId, scope, documentTypeKey, actor, correlationId, cancellationToken);
        }

        if (!supersededAny)
        {
            return Results.NotFound(new { message = "Nothing has been sent for that document yet." });
        }

        if (slotPdfService is not null)
        {
            await slotPdfService.RebuildAsync(scope, caseId, documentTypeKey, correlationId, cancellationToken);
        }

        var allDocuments = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, allDocuments, cancellationToken);
        var updatedCase = claimCase with
        {
            CompletenessScore = checklist.CompletenessScore.Percentage,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await caseRepository.SaveAsync(updatedCase, cancellationToken);

        var communications = await communicationRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(await WithPdfsAsync(ToResponse(updatedCase, checklist, allDocuments, communications), slotPdfRepository, scope, caseId, cancellationToken));
    }

    /// <summary>The customer rearranges the pages of a multi-page document. The pages and their files
    /// are unchanged; only their order is, and the slot's PDF is rebuilt to match.</summary>
    public static async Task<IResult> ReorderPages(
        Guid caseId,
        ClientPortalReorderPagesRequest body,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        ICommunicationRepository communicationRepository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        ISlotPdfService? slotPdfService = null,
        ISlotPdfRepository? slotPdfRepository = null)
    {
        if (!ResolveScope(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var documentTypeKey = NormalizeDocumentTypeKey(body?.DocumentTypeKey);
        if (documentTypeKey is null || body?.DocumentIds is null)
        {
            return Validation("documentIds", "Say which document and the order of its pages.");
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope || !BelongsToCustomer(claimCase, httpContext, options))
        {
            return Results.NotFound(new { message = "Claim was not found for the signed-in customer." });
        }

        var all = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var pages = SlotPdfService.PagesInOrder(all, documentTypeKey);
        var wanted = body.DocumentIds.ToArray();
        if (pages.Count == 0
            || wanted.Length != pages.Count
            || wanted.Distinct().Count() != wanted.Length
            || !pages.Select(page => page.DocumentId).ToHashSet().SetEquals(wanted))
        {
            return Validation("documentIds", "The new order must list every page of that document exactly once.");
        }

        for (var index = 0; index < wanted.Length; index++)
        {
            var page = pages.First(candidate => candidate.DocumentId == wanted[index]);
            if (page.PageOrder != index + 1)
            {
                await documentRepository.SaveAsync(page with { PageOrder = index + 1 }, cancellationToken);
            }
        }

        var actor = ResolveActor(httpContext);
        var correlationId = httpContext.TraceIdentifier;
        await auditRepository.WriteAsync(AuditEvent.SlotPagesChanged(scope, caseId, documentTypeKey, "document.pages.reordered", actor, correlationId), cancellationToken);
        if (slotPdfService is not null)
        {
            await slotPdfService.RebuildAsync(scope, caseId, documentTypeKey, correlationId, cancellationToken);
        }

        var allDocuments = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, allDocuments, cancellationToken);
        var communications = await communicationRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(await WithPdfsAsync(ToResponse(claimCase, checklist, allDocuments, communications), slotPdfRepository, scope, caseId, cancellationToken));
    }

    /// <summary>Fills in how many pages the PDF of each document slot has.</summary>
    private static async Task<ClientPortalClaimResponse> WithPdfsAsync(
        ClientPortalClaimResponse response,
        ISlotPdfRepository? slotPdfRepository,
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (slotPdfRepository is null)
        {
            return response;
        }

        var pdfs = await slotPdfRepository.ListForCaseAsync(scope, caseId, cancellationToken);
        if (pdfs.Count == 0)
        {
            return response;
        }

        return response with
        {
            RequiredDocuments = response.RequiredDocuments
                .Select(row => pdfs.FirstOrDefault(pdf => string.Equals(pdf.SlotKey, row.DocumentTypeKey, StringComparison.OrdinalIgnoreCase)) is { } pdf
                    ? row with { PageCount = pdf.PageCount }
                    : row)
                .ToArray(),
        };
    }

    /// <summary>Marks every currently-active (not already superseded) document matching this
    /// requirement key as superseded. Returns whether anything was actually superseded.</summary>
    private static async Task<bool> SupersedeActiveDocumentsAsync(
        ICaseDocumentRepository documentRepository,
        IAuditRepository auditRepository,
        Guid caseId,
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        string documentTypeKey,
        string actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var superseded = false;
        foreach (var document in existing)
        {
            if (document.IsSuperseded)
            {
                continue;
            }

            var key = document.DocumentTypeKey ?? document.DocumentType.ToString();
            if (!string.Equals(key, documentTypeKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var supersededDocument = document with { IsSuperseded = true, SupersededAt = now };
            await documentRepository.SaveAsync(supersededDocument, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.DocumentSuperseded(supersededDocument, actor, correlationId), cancellationToken);
            superseded = true;
        }

        return superseded;
    }

    private static string? NormalizeDocumentTypeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClientPortalClaimResponse ToResponse(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        IReadOnlyCollection<CaseDocument> documents,
        IReadOnlyCollection<Communication> communications,
        IReadOnlyCollection<ClaimReviewRequest>? reviewRequests = null)
    {
        var activeDocuments = documents.Where(document => !document.IsSuperseded).ToArray();

        ClientPortalDocumentRequirement RowFor(ResolvedDocumentSlot slot)
        {
            var inSlot = activeDocuments
                .Where(document => string.Equals(document.EffectiveKey, slot.Key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(document => document.UploadedAt)
                .ToArray();
            var latest = inSlot.LastOrDefault();
            var failed = inSlot.Any(document => document.ProcessingStatus == DocumentProcessingStatus.Failed);
            var allProcessed = inSlot.Length > 0 && inSlot.All(document =>
                document.ProcessingStatus is DocumentProcessingStatus.Processed or DocumentProcessingStatus.ExtractionApproved
                    or DocumentProcessingStatus.ReviewRequired or DocumentProcessingStatus.Reviewed);
            var status = inSlot.Length == 0 ? "Not uploaded" : failed ? "Could not be read" : allProcessed ? "Received" : "Being checked";
            // A disagreement between the customer's choice and the classifier's independent guess
            // is flagged for staff, never shown to the customer as an error (outcome B).
            var disagreesWithClassifier = inSlot.Any(document => document.DisagreesWithClassifier);

            return new ClientPortalDocumentRequirement(
                slot.Key,
                slot.Label,
                slot.Description,
                slot.Required,
                inSlot.Length > 0,
                inSlot.Any(document => document.CountsTowardsCompleteness),
                status,
                latest?.OriginalFileName,
                latest?.UploadedAt,
                disagreesWithClassifier,
                CanRetry: failed,
                Kind: slot.Kind.ToString(),
                AllowsMultipleFiles: slot.AllowsMultipleFiles,
                MaxFiles: slot.MaxFiles,
                AcceptedFormats: slot.AcceptedFormats,
                FileCount: inSlot.Length,
                Files: SlotPdfService.PagesInOrder(inSlot, slot.Key)
                    .Select(document => new ClientPortalSlotFile(document.DocumentId, document.OriginalFileName, document.ProcessingStatus.ToString(), document.UploadedAt))
                    .ToArray());
        }

        // The slots come from the claim's pinned product and claim type; a checklist built by code
        // that predates slots falls back to the built-in rules with their default wording.
        var slots = checklist.Slots.Count > 0
            ? checklist.Slots
            : new DocumentChecklistRuleSet(
                checklist.RequiredDocuments, checklist.OptionalDocuments, checklist.CustomRequired, checklist.CustomOptional).Slots;
        var requiredDocuments = slots.Select(RowFor).ToArray();

        var messages = communications
            .Where(communication => IsCustomerSafeMessage(communication.MessageBody))
            .OrderByDescending(communication => communication.CreatedAt)
            .Take(10)
            .Select(communication => new ClientPortalMessage(
                communication.CommunicationId,
                communication.Channel.ToString(),
                communication.Direction,
                communication.MessageBody,
                communication.CreatedAt,
                communication.DeliveryStatus))
            .ToArray();

        var reviewRequestSummaries = (reviewRequests ?? Array.Empty<ClaimReviewRequest>())
            .OrderByDescending(request => request.RequestedAt)
            .Select(request => new ClientPortalReviewRequestSummary(
                request.RequestId,
                request.Status.ToString(),
                request.Reason,
                request.RequestedAt,
                request.ResolvedAt))
            .ToArray();

        return new ClientPortalClaimResponse(
            claimCase.CaseId,
            claimCase.ProductLine,
            claimCase.ClaimType,
            claimCase.Status.ToString(),
            claimCase.PolicyNumber,
            claimCase.IncidentDate,
            claimCase.CreatedAt,
            claimCase.UpdatedAt,
            checklist.CompletenessScore.Percentage,
            checklist.CompletenessScore.RequiredReceived,
            checklist.CompletenessScore.RequiredTotal,
            requiredDocuments,
            BuildTimeline(claimCase),
            messages,
            BuildNextSteps(checklist),
            reviewRequestSummaries);
    }

    private static IReadOnlyCollection<ClientPortalTimelineStep> BuildTimeline(ClaimCase claimCase)
    {
        var underReview = claimCase.Status is not CaseStatus.New;
        var assessment = claimCase.Status is CaseStatus.ReadyForReview or CaseStatus.Escalated or CaseStatus.Settled or CaseStatus.Closed;
        var decision = claimCase.Status is CaseStatus.Settled or CaseStatus.Closed;

        return
        [
            new("Claim received", true, claimCase.CreatedAt),
            new("Under review", underReview, underReview ? claimCase.UpdatedAt : null),
            new("Assessment", assessment, null),
            new("Decision", decision, null),
            new("Settlement", claimCase.Status is CaseStatus.Settled or CaseStatus.Closed, null)
        ];
    }

    private static IReadOnlyCollection<string> BuildNextSteps(DocumentCompletenessChecklist checklist)
    {
        if (checklist.MissingRequiredDocuments.Count > 0)
        {
            return
            [
                "Upload the remaining required documents.",
                "Our team will review the information received.",
                "We will message you with the next update."
            ];
        }

        return
        [
            "No required documents are missing right now.",
            "Our team will complete the claim review.",
            "We will message you with the decision."
        ];
    }

    /// <summary>
    /// Resolves tenant scope for a claimant-facing request. Tries the staff resolution path first
    /// (Entra claims in protected mode, or demo headers in Development), this preserves every
    /// existing behavior unchanged. A claimant reaching the portal through a magic link carries
    /// neither, so this falls back to reading X-Tenant-Id/X-Workspace-Id headers directly
    /// (ClientPortalScopeReader): safe here because knowing a tenant id alone grants no case
    /// access, BelongsToCustomer below is the actual gate.
    /// </summary>
    private static bool ResolveScope(HttpContext httpContext, out InsuranceIntelligence.Domain.Common.TenantScope scope, out string validationError) =>
        WorkflowEndpoints.TryResolveScope(httpContext, out scope, out _, out validationError)
        || ClientPortalScopeReader.TryGetFromHeaders(httpContext, out scope, out validationError);

    /// <summary>
    /// The claimant identity for this request, preferring a server-verified magic-link session
    /// (stashed by ClaimantSessionEndpointFilter) over anything the browser sent as a header.
    /// </summary>
    internal static string ResolveActor(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.ContactValueItemKey, out var stashed) && stashed is string verified
            ? verified
            : WorkflowEndpoints.GetActor(httpContext, "client-portal-user");

    /// <summary>
    /// Three trust tiers, highest first: (1) a verified magic-link session (stashed by
    /// ClaimantSessionEndpointFilter), the intended path for a real claimant; (2) a genuinely
    /// authenticated principal or a server-resolved ITenantContextAccessor, neither of which a
    /// browser can spoof by sending a header; (3) outside Development, nothing else is trusted, a
    /// raw X-User-Id/x-demo-user-id header is never sufficient on its own, mirroring
    /// DemoAuthorization.RequireAny's fail-closed treatment of demo headers outside Development.
    /// Before this method took `options`, WorkflowEndpoints.GetActor's raw-header fallback was
    /// reachable unconditionally, an unauthenticated request could set X-User-Id to any claimant's
    /// email and read their case in any AuthMode. Tier 3 closes that.
    /// </summary>
    internal static bool BelongsToCustomer(ClaimCase claimCase, HttpContext httpContext, InfrastructureOptions options)
    {
        if (httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.ContactValueItemKey, out var stashed) && stashed is string verified)
        {
            return ClaimantContactMatcher.Matches(verified, claimCase.CustomerEmail, claimCase.CustomerPhone, claimCase.CreatedByUserId);
        }

        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
        var hasResolvedTenantContext = httpContext.RequestServices.GetService(typeof(ITenantContextAccessor))
            is ITenantContextAccessor { HasCurrent: true };
        if (!options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && !isAuthenticated
            && !hasResolvedTenantContext)
        {
            return false;
        }

        var userId = WorkflowEndpoints.GetActor(httpContext, string.Empty);
        return !string.IsNullOrWhiteSpace(userId)
            && ClaimantContactMatcher.Matches(userId, claimCase.CustomerEmail, claimCase.CustomerPhone, claimCase.CreatedByUserId);
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

    private static async Task LogDisclosureAsync(
        IAuditRepository? auditRepository,
        HttpContext httpContext,
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        Guid? caseId,
        string disclosureType,
        CancellationToken cancellationToken,
        ClaimantPortalSession? session = null)
    {
        if (auditRepository is null)
        {
            return;
        }

        var sessionId = session?.SessionId ?? Guid.Empty;
        var contactValue = session?.ContactValue ?? ResolveActor(httpContext);
        await auditRepository.WriteAsync(
            AuditEvent.ClaimantDisclosureViewed(scope, caseId, sessionId, contactValue, disclosureType, GetCorrelationId(httpContext)),
            cancellationToken);
    }

    private static async Task<AssistantAnswer> AnswerQuestionAsync(
        IRetrievalProvider retrievalProvider,
        IAiAssistantProvider assistantProvider,
        AssistantQuestion question,
        CancellationToken cancellationToken)
    {
        var retrieval = await retrievalProvider.RetrieveAsync(question, cancellationToken);
        return await assistantProvider.AnswerPolicyQuestionAsync(question, retrieval, cancellationToken);
    }

    private static string SummarizeInput(string question) =>
        question.Length <= 180 ? question : $"{question[..180]}...";

    private static ClientPortalPolicyResponse ToPolicyResponse(Policy policy, IReadOnlyCollection<Coverage> coverages) =>
        new(
            policy.PolicyNumber,
            policy.ProductLine,
            policy.CoverType,
            policy.EffectiveFrom,
            policy.EffectiveTo,
            PolicyStanding.Resolve(policy.EffectiveFrom, policy.EffectiveTo, DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date)).ToString(),
            policy.AnnualPremium,
            policy.PremiumCurrency,
            policy.SumInsured,
            "NotConfigured",
            coverages
                .Select(coverage => new ClientPortalCoverageResponse(
                    coverage.Name, coverage.Limit, coverage.Deductible, coverage.InsuredObject, coverage.Description))
                .ToArray(),
            policy.RecordSource.ToString(),
            policy.LastRefreshedAt);

    private static bool IsCustomerSafeMessage(string value) =>
        !new[] { "raw prompt", "system message", "internal note", "fraud flag", "reserve", "hidden risk", "secret", "confidence" }
            .Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static CaseDocument CreateUploadedDocument(
        Guid documentId,
        ClaimCase claimCase,
        IFormFile file,
        DocumentStorageResult storageResult,
        string uploadedBy,
        string correlationId,
        string? documentTypeKey = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new CaseDocument
        {
            DocumentId = documentId,
            CaseId = claimCase.CaseId,
            Scope = claimCase.Scope,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = storageResult.ContentType,
            FileSizeBytes = storageResult.FileSizeBytes,
            ObjectKey = storageResult.ObjectKey,
            DocumentType = DocumentType.Unknown,
            DocumentTypeKey = documentTypeKey,
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "ClientPortal", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "ClientPortal",
                now,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = uploadedBy,
            UploadedAt = now,
            CorrelationId = correlationId
        };
    }

    /// <summary>Why these files cannot go into this slot, in words for the customer, or null.</summary>
    internal static string? SlotUploadError(ResolvedDocumentSlot slot, IReadOnlyList<IFormFile> files, IReadOnlyCollection<CaseDocument> existing)
    {
        foreach (var file in files)
        {
            if (!slot.Accepts(file.FileName))
            {
                return $"This one takes {FormatList(slot.AcceptedFormats)} files. {Path.GetFileName(file.FileName)} is a different kind of file.";
            }
        }

        if (!slot.AllowsMultipleFiles)
        {
            return files.Count > 1 ? "Send one file for this document." : null;
        }

        var alreadyThere = existing.Count(document => !document.IsSuperseded
            && document.ProcessingStatus != DocumentProcessingStatus.Failed
            && string.Equals(document.EffectiveKey, slot.Key, StringComparison.OrdinalIgnoreCase));
        if (alreadyThere + files.Count > slot.MaxFiles)
        {
            var room = Math.Max(0, slot.MaxFiles - alreadyThere);
            return room == 0
                ? $"You have already sent the most this can hold ({slot.MaxFiles}). Remove one to add another."
                : $"You can add {room} more here (up to {slot.MaxFiles}).";
        }

        return null;
    }

    private static string FormatList(IReadOnlyList<string> formats) =>
        string.Join(", ", formats.Select(format => format.ToUpperInvariant()));

    private static string? ValidateUploadFile(IFormFile file) =>
        DocumentUploadValidation.Validate(file.Length, file.FileName);

    private static bool IsMultipartFormData(string? contentType) =>
        contentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizeContentType(IFormFile file) =>
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

public sealed record ClientPortalClaimResponse(
    Guid CaseId,
    string ProductLine,
    string ClaimType,
    string Status,
    string PolicyNumber,
    DateOnly IncidentDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal CompletenessScore,
    int RequiredDocumentsReceived,
    int RequiredDocumentsTotal,
    // Every row from the claim's pinned product version, required and optional alike, each
    // carrying its own Required flag (outcome B); not only the required ones despite the name
    // this field has always had.
    IReadOnlyCollection<ClientPortalDocumentRequirement> RequiredDocuments,
    IReadOnlyCollection<ClientPortalTimelineStep> Timeline,
    IReadOnlyCollection<ClientPortalMessage> Messages,
    IReadOnlyCollection<string> NextSteps,
    IReadOnlyCollection<ClientPortalReviewRequestSummary> ReviewRequests);

public sealed record ClientPortalReviewRequestBody(string Reason);

public sealed record ClientPortalReviewRequestResponse(
    Guid RequestId,
    string Status,
    DateTimeOffset RequestedAt,
    string Message);

public sealed record ClientPortalReviewRequestSummary(
    Guid RequestId,
    string Status,
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt);

public sealed record ClientPortalDocumentRequirement(
    string DocumentTypeKey,
    string Label,
    string Description,
    bool Required,
    bool Uploaded,
    bool Received,
    string Status,
    string? FileName,
    DateTimeOffset? UploadedAt,
    bool DisagreesWithClassifier,
    bool CanRetry = false,
    // What this slot holds and allows (build-loop item 4). Kind is "Document" (paperwork, can be
    // scanned and turned into a PDF) or "Evidence" (kept exactly as taken).
    string Kind = "Document",
    bool AllowsMultipleFiles = false,
    int MaxFiles = 1,
    IReadOnlyList<string>? AcceptedFormats = null,
    int FileCount = 0,
    IReadOnlyList<ClientPortalSlotFile>? Files = null,
    // Pages in the one PDF made from this slot's pictures (build-loop item 12); 0 when there is none.
    int PageCount = 0);

public sealed record ClientPortalSlotFile(Guid DocumentId, string FileName, string Status, DateTimeOffset UploadedAt);

/// <summary>Which document to remove or retry: the slot, and optionally one page of it. Without a
/// page, the whole slot is meant.</summary>
public sealed record ClientPortalRemoveDocumentRequest(string? DocumentTypeKey, Guid? DocumentId = null);

/// <summary>The pages of a multi-page slot in the order the customer wants them, first page first.
/// Must name every page the slot holds, once each.</summary>
public sealed record ClientPortalReorderPagesRequest(string? DocumentTypeKey, IReadOnlyList<Guid>? DocumentIds);

public sealed record ClientPortalTimelineStep(
    string Label,
    bool Complete,
    DateTimeOffset? Date);

public sealed record ClientPortalMessage(
    Guid CommunicationId,
    string Channel,
    string Direction,
    string MessageBody,
    DateTimeOffset CreatedAt,
    string DeliveryStatus);

public sealed record ClientPortalPolicyResponse(
    string PolicyNumber,
    string ProductLine,
    string? CoverType,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string CoverStatus,
    decimal? AnnualPremium,
    string? PremiumCurrency,
    decimal? SumInsured,
    string PremiumStatus,
    IReadOnlyCollection<ClientPortalCoverageResponse> Coverages,
    // Outcome N acceptance: provenance stays visible on the customer's own view of their policy,
    // not just the staff one.
    string RecordSource,
    DateTimeOffset? LastRefreshedAt);

public sealed record ClientPortalCoverageResponse(
    string Name,
    string Limit,
    string Deductible,
    string InsuredObject,
    string Description);

public sealed record ClientPortalPolicyQuestionRequest(string Question);

public sealed record ClientPortalPolicyQuestionResponse(
    string AnswerText,
    bool EscalationRequired,
    IReadOnlyCollection<SourceCitationResponse> Sources);

public sealed record ClientPortalContactDetailChangeRequestBody(string? Email, string? Phone, string? Note);

public sealed record ClientPortalContactDetailChangeResponse(
    Guid RequestId,
    string Status,
    DateTimeOffset RequestedAt,
    string Message);
