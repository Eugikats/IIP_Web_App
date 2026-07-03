using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.CommandCenter;

public interface ICommandCenterQueryService
{
    Task<CommandCenterQueryResponse> QueryAsync(
        CommandCenterQueryRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class CommandCenterQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICaseRepository caseRepository,
    ICaseDocumentRepository caseDocumentRepository,
    IDocumentChecklistProvider checklistProvider,
    IKnowledgeDocumentRepository knowledgeRepository,
    IRetrievalProvider retrievalProvider,
    IAgentActionLogRepository actionLogRepository,
    IApprovalRepository approvalRepository,
    IWorkflowTaskRepository taskRepository,
    ICustomerUpdateRepository updateRepository,
    IAuditRepository auditRepository,
    ISlaCalculator slaCalculator,
    IIntentClassifier intentClassifier,
    IProviderRequirementResolver providerRequirementResolver,
    IProviderHealthCheck providerHealthCheck,
    IChatCompletionProvider chatCompletionProvider,
    IAiOutputValidator aiOutputValidator,
    INotificationDraftService notificationDraftService) : ICommandCenterQueryService
{
    private const string ModelProvider = "Deterministic";
    private const string PromptVersion = "command-center-query-v1";
    private const string Visibility = "Internal";

    public async Task<CommandCenterQueryResponse> QueryAsync(
        CommandCenterQueryRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new CommandCenterValidationException("question", "A Command Center question is required.");
        }

        var question = request.Question.Trim();
        if (question.Length > 2000)
        {
            throw new CommandCenterValidationException("question", "A Command Center question must be 2,000 characters or fewer.");
        }

        await permissionService.RequirePermissionAsync(Permission.ViewCommandCenter, cancellationToken);

        var context = tenantContextAccessor.Current;
        var intent = await intentClassifier.ClassifyAsync(
            question,
            context.TenantId,
            context.WorkspaceId,
            cancellationToken);

        await RequireIntentPermissionAsync(intent, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var providerGate = await EnsureRequiredProvidersAsync(intent.Intent, cancellationToken);
        if (providerGate is not null)
        {
            return await BuildNotConfiguredResponseAsync(
                question,
                context,
                providerGate,
                correlationId,
                now,
                cancellationToken);
        }

        if (intent.Intent == IntentType.PrepareCustomerUpdateDraft)
        {
            return await BuildCustomerUpdateDraftResponseAsync(
                question,
                context,
                intent.Parameters,
                correlationId,
                now,
                cancellationToken);
        }

        var scope = context.Scope;
        var data = await LoadQueryDataAsync(scope, now, cancellationToken);

        if (intent.Intent == IntentType.GenerateManagementSummary)
        {
            return await BuildManagementSummaryResponseAsync(
                question,
                context,
                intent.Parameters,
                data,
                correlationId,
                now,
                cancellationToken);
        }

        var blocks = CommandCenterQueryPlanner.Plan(question, data);
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = null,
            UserId = context.UserId,
            AgentName = "CommandCenterQuery",
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = SummarizeInput(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = $"Command Center briefing: {blocks.Count} block(s) over {data.Cases.Count} claim(s).",
            ConfidenceScore = AiConfidenceScore.Create(1.0m),
            RiskFlag = "none",
            HumanOverride = false,
            CreatedAt = now,
            CorrelationId = correlationId,
            ModelProvider = ModelProvider,
            ModelDeployment = null,
            PromptVersion = PromptVersion,
            Visibility = Visibility,
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);

        return new CommandCenterQueryResponse(
            blocks,
            actionLog.AgentActionLogId,
            correlationId,
            ModelProvider,
            PromptVersion,
            Visibility,
            CanTriggerDecision: false,
            now);
    }

    private async Task<CommandCenterQueryData> LoadQueryDataAsync(
        Domain.Common.TenantScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var metrics = await LoadMetricsAsync(scope, now, cancellationToken);
        var cases = await caseRepository.ListAsync(scope, cancellationToken);

        var caseViews = new List<CommandCenterCaseView>(cases.Count);
        foreach (var claimCase in cases)
        {
            var documents = await caseDocumentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
            var missing = checklist.MissingRequiredDocuments.Select(item => item.Label).ToList();
            var slaStatus = slaCalculator.Calculate(claimCase, now);
            var (token, dueLabel) = CommandCenterQueryPlanner.ResolveSla(claimCase, slaStatus, now);

            caseViews.Add(new CommandCenterCaseView(
                claimCase.CaseId,
                claimCase.CustomerName,
                claimCase.ProductLine,
                claimCase.Status.ToString(),
                slaStatus,
                token,
                dueLabel,
                claimCase.CompletenessScore,
                claimCase.AssignedUserId ?? claimCase.AssignedTeam ?? "Unassigned",
                missing));
        }

        var approvals = await approvalRepository.ListAsync(scope, cancellationToken);
        var pendingApprovals = approvals
            .Where(approval => approval.Status == ApprovalStatus.Pending)
            .Select(approval => new CommandCenterApprovalView(
                approval.ApprovalId,
                approval.CaseId,
                approval.ApprovalType,
                "Pending"))
            .ToList();

        return new CommandCenterQueryData(
            metrics,
            caseViews,
            pendingApprovals,
            Array.Empty<CommandCenterProviderStatusItem>());
    }

    private async Task<CommandCenterMetricsResponse> LoadMetricsAsync(
        Domain.Common.TenantScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var documentCount = 0;
        foreach (var claimCase in cases)
        {
            var documents = await caseDocumentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            documentCount += documents.Count;
        }

        var knowledgeDocuments = await knowledgeRepository.ListAsync(scope, cancellationToken);
        var actionLogs = await actionLogRepository.ListAsync(scope, cancellationToken);
        var approvals = await approvalRepository.ListAsync(scope, cancellationToken);
        var tasks = await taskRepository.ListAsync(scope, cancellationToken);
        var updates = await updateRepository.ListAsync(scope, cancellationToken);
        var slaStatuses = cases.Select(claimCase => slaCalculator.Calculate(claimCase, now)).ToArray();
        var today = now.Date;
        var averageCompleteness = cases.Count == 0
            ? 0m
            : Math.Round(cases.Average(claimCase => claimCase.CompletenessScore), 2);

        return new CommandCenterMetricsResponse(
            cases.Count,
            documentCount,
            averageCompleteness,
            knowledgeDocuments.Count(document => document.ApprovalStatus == KnowledgeDocumentStatus.Approved),
            actionLogs.Count,
            actionLogs.Count(actionLog => actionLog.RiskFlag is "no_source" or "needs_review"),
            cases.Count(claimCase => claimCase.Status != CaseStatus.Closed),
            cases.Count(claimCase => claimCase.CreatedAt.Date == today),
            cases.Count(claimCase => claimCase.CompletenessScore < 100m),
            approvals.Count(approval => approval.Status == ApprovalStatus.Pending),
            tasks.Count(task => task.Status is WorkflowTaskStatus.Open or WorkflowTaskStatus.InProgress),
            updates.Count(update => update.Status is CustomerUpdateStatus.Draft or CustomerUpdateStatus.PendingApproval or CustomerUpdateStatus.Approved),
            slaStatuses.Count(status => status == SlaStatus.AtRisk),
            slaStatuses.Count(status => status == SlaStatus.Breached));
    }

    private async Task RequireIntentPermissionAsync(IntentClassificationResult intent, CancellationToken cancellationToken)
    {
        switch (intent.Intent)
        {
            case IntentType.ShowPendingApprovals:
                await permissionService.RequirePermissionAsync(Permission.ApproveSettlementWorkflowStep, cancellationToken);
                break;
            case IntentType.ShowAttentionClaims:
            case IntentType.ShowMissingDocuments:
            case IntentType.ShowSlaBreaches:
            case IntentType.GenerateManagementSummary:
            case IntentType.PrepareCustomerUpdateDraft:
            case IntentType.Unsupported:
                await RequireClaimsReadPermissionAsync(cancellationToken);
                break;
            default:
                break;
        }
    }

    private async Task RequireClaimsReadPermissionAsync(CancellationToken cancellationToken)
    {
        if (await permissionService.HasPermissionAsync(Permission.ViewAssignedClaims, cancellationToken)
            || await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken))
        {
            return;
        }

        throw new PermissionDeniedException(Permission.ViewAssignedClaims);
    }

    private async Task<ProviderGateResult?> EnsureRequiredProvidersAsync(
        IntentType intent,
        CancellationToken cancellationToken)
    {
        foreach (var provider in providerRequirementResolver.GetRequiredProviders(intent))
        {
            var health = await providerHealthCheck.CheckAsync(provider.ProviderCode, cancellationToken);
            if (!health.Status.Equals("ready", StringComparison.OrdinalIgnoreCase))
            {
                return new ProviderGateResult(
                    provider.DisplayName,
                    $"{provider.DisplayName} is not configured or not ready.",
                    string.IsNullOrWhiteSpace(health.Detail) ? "Configure the provider in Admin > Integrations." : health.Detail);
            }
        }

        return null;
    }

    private async Task<CommandCenterQueryResponse> BuildNotConfiguredResponseAsync(
        string question,
        TenantContext context,
        ProviderGateResult providerGate,
        string correlationId,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        var block = new NotConfiguredBlock(providerGate.Provider, providerGate.AdminAction);
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = context.Scope,
            CaseId = null,
            UserId = context.UserId,
            AgentName = "CommandCenterQuery",
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = SummarizeInput(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = $"Command Center query returned not_configured for {providerGate.Provider}.",
            ConfidenceScore = AiConfidenceScore.Create(0m),
            RiskFlag = "provider_not_configured",
            HumanOverride = false,
            CreatedAt = generatedAt,
            CorrelationId = correlationId,
            ModelProvider = ModelProvider,
            ModelDeployment = null,
            PromptVersion = PromptVersion,
            Visibility = Visibility,
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);

        return new CommandCenterQueryResponse(
            [block],
            actionLog.AgentActionLogId,
            correlationId,
            ModelProvider,
            PromptVersion,
            Visibility,
            CanTriggerDecision: false,
            generatedAt);
    }

    private async Task<CommandCenterQueryResponse> BuildCustomerUpdateDraftResponseAsync(
        string question,
        TenantContext context,
        IntentParameters parameters,
        string correlationId,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        if (!parameters.TryGetString("claimId", out var rawCaseId) || !Guid.TryParse(rawCaseId, out var caseId))
        {
            return await BuildErrorResponseAsync(
                question,
                context,
                "missing_claim_id",
                "I need a specific claim ID before I can prepare a customer update draft.",
                correlationId,
                generatedAt,
                cancellationToken);
        }

        var scope = context.Scope;
        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope)
        {
            return await BuildErrorResponseAsync(
                question,
                context,
                "claim_not_found",
                "The claim was not found or is not visible in this tenant workspace.",
                correlationId,
                generatedAt,
                cancellationToken);
        }

        var documents = await caseDocumentRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var missingDocuments = checklist.MissingRequiredDocuments.Count == 0
            ? "none"
            : string.Join(", ", checklist.MissingRequiredDocuments.Select(document => document.Label));
        var receivedDocuments = checklist.ReceivedDocuments.Count == 0
            ? "none"
            : string.Join(", ", checklist.ReceivedDocuments.Select(document => document.ToString()));

        var prompt = string.Join('\n', [
            "Draft a concise customer-safe insurance claim update for human review.",
            $"Customer: {claimCase.CustomerName}",
            $"Policy number: {claimCase.PolicyNumber}",
            $"Product line: {claimCase.ProductLine}",
            $"Claim type: {claimCase.ClaimType}",
            $"Claim status: {claimCase.Status}",
            $"Missing required documents: {missingDocuments}",
            $"Received documents: {receivedDocuments}",
            "Rules: do not deny or approve the claim, promise payment, admit liability, accuse fraud, disclose reserves, disclose internal notes, or present this as sent."
        ]);

        var chatResult = await chatCompletionProvider.CompleteAsync(
            new ChatCompletionRequest(prompt, context.TenantId, context.WorkspaceId, correlationId),
            cancellationToken);
        var validated = await aiOutputValidator.ValidateCustomerSafeDraftAsync(
            chatResult.Content,
            context,
            cancellationToken);

        var draft = await notificationDraftService.CreateDraftAsync(
            new NotificationDraftCreateRequest(
                context.TenantId,
                context.WorkspaceId,
                "claim",
                claimCase.CaseId.ToString(),
                "Email",
                validated.Preview,
                RequiresApproval: true,
                context.UserId,
                correlationId),
            cancellationToken);

        var blocks = new CommandCenterResponseBlock[]
        {
            new TextBlock("Draft customer update prepared. Human review is required before any message is sent."),
            new DraftCommunicationBlock(draft.DraftId, validated.Preview, RequiresApproval: true),
            new ActionProposalBlock([
                new ProposedAction(
                    "Review draft",
                    "Open the persisted customer update draft for staff review.",
                    $"/customer-updates/{draft.DraftId}"),
                new ProposedAction(
                    "Submit for approval",
                    "Send the draft into the governed approval workflow before delivery.",
                    $"/customer-updates/{draft.DraftId}/approval")
            ]),
            new EvidenceListBlock([
                new EvidenceSource(
                    $"Claim {claimCase.CaseId}",
                    claimCase.CustomerName,
                    $"/claims/{claimCase.CaseId}")
            ])
        };

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = claimCase.CaseId,
            UserId = context.UserId,
            AgentName = "CommandCenterQuery",
            AssistantMode = AssistantMode.CustomerUpdateDrafting,
            InputSummary = SummarizeInput(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = $"Customer update draft {draft.DraftId} persisted for human review.",
            ConfidenceScore = AiConfidenceScore.Create(0.75m),
            RiskFlag = validated.GuardrailsApplied.Count == 0 ? "needs_review" : string.Join(",", validated.GuardrailsApplied),
            HumanOverride = false,
            CreatedAt = generatedAt,
            CorrelationId = correlationId,
            ModelProvider = chatResult.Provider,
            ModelDeployment = chatResult.Deployment,
            PromptVersion = PromptVersion,
            Visibility = Visibility,
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);

        return new CommandCenterQueryResponse(
            blocks,
            actionLog.AgentActionLogId,
            correlationId,
            chatResult.Provider,
            PromptVersion,
            Visibility,
            CanTriggerDecision: false,
            generatedAt);
    }

    private async Task<CommandCenterQueryResponse> BuildManagementSummaryResponseAsync(
        string question,
        TenantContext context,
        IntentParameters parameters,
        CommandCenterQueryData data,
        string correlationId,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        var retrievalResult = await retrievalProvider.RetrieveAsync(
            new AssistantQuestion(
                context.Scope,
                CaseId: null,
                "Claims operations management summary guidance",
                ProductLine: null,
                Audience: "Internal",
                context.UserId,
                correlationId),
            cancellationToken);

        var sourceContext = retrievalResult.Citations.Count == 0
            ? "No approved management-summary guidance was retrieved."
            : string.Join("\n", retrievalResult.Citations.Take(5).Select(citation =>
                $"- {citation.DocumentName} {citation.Version}: {citation.Excerpt}"));
        var topCases = data.Cases
            .OrderBy(view => view.SlaToken)
            .ThenBy(view => view.Completeness)
            .Take(5)
            .Select(view => $"- {view.Customer}: {view.Status}, SLA {view.SlaToken}, completeness {view.Completeness:0.##}%, missing {string.Join(", ", view.MissingDocuments.DefaultIfEmpty("none"))}");
        var metrics = data.Metrics;

        var prompt = string.Join('\n', [
            "Generate a concise management summary for today's insurance claims operations.",
            "Use only the operational figures and approved source context supplied here. Do not invent numbers.",
            "Do not make final claim, settlement, payment, underwriting, complaint, or fraud decisions.",
            $"Open claims: {metrics.OpenClaims}",
            $"New claims today: {metrics.NewClaimsToday}",
            $"Incomplete claims: {metrics.IncompleteClaims}",
            $"Pending approvals: {metrics.PendingApprovals}",
            $"SLA at risk: {metrics.SlaAtRisk}",
            $"SLA breached: {metrics.SlaBreached}",
            $"Customer updates pending: {metrics.CustomerUpdatesPending}",
            "Top operational cases:",
            string.Join('\n', topCases),
            "Approved source context:",
            sourceContext,
            $"Additional user context: {parameters.GetStringOrDefault("additionalContext") ?? "None"}"
        ]);

        var chatResult = await chatCompletionProvider.CompleteAsync(
            new ChatCompletionRequest(prompt, context.TenantId, context.WorkspaceId, correlationId),
            cancellationToken);
        var validated = await aiOutputValidator.ValidateManagementSummaryAsync(
            chatResult.Content,
            context,
            cancellationToken);

        var sections = ToSummarySections(validated.Preview);
        var blocks = new List<CommandCenterResponseBlock>
        {
            new TextBlock("AI-generated management summary prepared for human review from current operational data."),
            new ManagementSummaryBlock("Daily claims operations summary", sections),
            new MetricPillsBlock([
                new MetricPill("Open claims", metrics.OpenClaims.ToString(), CommandCenterBlockVocabulary.ToneInfo),
                new MetricPill("New today", metrics.NewClaimsToday.ToString(), CommandCenterBlockVocabulary.ToneInfo),
                new MetricPill("SLA at risk", metrics.SlaAtRisk.ToString(), metrics.SlaAtRisk > 0 ? CommandCenterBlockVocabulary.ToneWarning : CommandCenterBlockVocabulary.ToneNeutral),
                new MetricPill("SLA breached", metrics.SlaBreached.ToString(), metrics.SlaBreached > 0 ? CommandCenterBlockVocabulary.ToneDanger : CommandCenterBlockVocabulary.ToneNeutral),
                new MetricPill("Pending approvals", metrics.PendingApprovals.ToString(), CommandCenterBlockVocabulary.ToneInfo)
            ])
        };

        if (retrievalResult.Citations.Count > 0)
        {
            blocks.Add(new EvidenceListBlock(retrievalResult.Citations
                .Take(5)
                .Select(citation => new EvidenceSource(
                    citation.Label,
                    citation.DocumentName,
                    $"/knowledge-documents/{citation.KnowledgeDocumentId}#{citation.ChunkId}"))
                .ToList()));
        }

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = context.Scope,
            CaseId = null,
            UserId = context.UserId,
            AgentName = "CommandCenterQuery",
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = SummarizeInput(question),
            RetrievedSources = retrievalResult.Citations.ToArray(),
            ModelOutput = $"Management summary generated from {data.Cases.Count} claim(s), {retrievalResult.Citations.Count} citation(s), and {sections.Count} section(s).",
            ConfidenceScore = AiConfidenceScore.Create(retrievalResult.Citations.Count > 0 ? 0.78m : 0.62m),
            RiskFlag = validated.GuardrailsApplied.Count == 0 ? "needs_review" : string.Join(",", validated.GuardrailsApplied),
            HumanOverride = false,
            CreatedAt = generatedAt,
            CorrelationId = correlationId,
            ModelProvider = chatResult.Provider,
            ModelDeployment = chatResult.Deployment,
            PromptVersion = PromptVersion,
            Visibility = Visibility,
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);

        return new CommandCenterQueryResponse(
            blocks,
            actionLog.AgentActionLogId,
            correlationId,
            chatResult.Provider,
            PromptVersion,
            Visibility,
            CanTriggerDecision: false,
            generatedAt);
    }

    private static IReadOnlyList<SummarySection> ToSummarySections(string preview)
    {
        var sentences = (preview ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (sentences.Length == 0)
        {
            return [new SummarySection("Summary", "No summary content was produced after validation. Human review is required.")];
        }

        if (sentences.Length == 1)
        {
            return [new SummarySection("Summary", sentences[0])];
        }

        return sentences
            .Take(4)
            .Select((line, index) => new SummarySection(index == 0 ? "Overview" : $"Point {index + 1}", line))
            .ToArray();
    }

    private async Task<CommandCenterQueryResponse> BuildErrorResponseAsync(
        string question,
        TenantContext context,
        string code,
        string safeMessage,
        string correlationId,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = context.Scope,
            CaseId = null,
            UserId = context.UserId,
            AgentName = "CommandCenterQuery",
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = SummarizeInput(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = $"Command Center query returned safe error {code}.",
            ConfidenceScore = AiConfidenceScore.Create(0m),
            RiskFlag = code,
            HumanOverride = false,
            CreatedAt = generatedAt,
            CorrelationId = correlationId,
            ModelProvider = ModelProvider,
            ModelDeployment = null,
            PromptVersion = PromptVersion,
            Visibility = Visibility,
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);

        return new CommandCenterQueryResponse(
            [new ErrorBlock(code, safeMessage, correlationId)],
            actionLog.AgentActionLogId,
            correlationId,
            ModelProvider,
            PromptVersion,
            Visibility,
            CanTriggerDecision: false,
            generatedAt);
    }

    private static string SummarizeInput(string question) =>
        question.Length <= 180 ? question : $"{question[..180]}...";
}

public sealed class CommandCenterValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

public sealed record ProviderGateResult(string Provider, string Message, string AdminAction);
