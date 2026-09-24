using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
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

// WO-09: knowledgeRepository, approvalRepository, taskRepository, updateRepository,
// slaCalculator, and providerHealthCheck are no longer injected here, their data access moved
// into the WO-08 tool registry (claims.attention.read, approvals.queue.read, metrics.read,
// provider_health.read). caseRepository/caseDocumentRepository/retrievalProvider/
// actionLogRepository remain: they're still used directly for single-case lookups
// (BuildCustomerUpdateDraftResponseAsync) and the management-summary guidance retrieval
// (BuildManagementSummaryResponseAsync), neither of which this work order routed through a tool.
public sealed class CommandCenterQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICaseRepository caseRepository,
    ICaseDocumentRepository caseDocumentRepository,
    IDocumentChecklistProvider checklistProvider,
    IRetrievalProvider retrievalProvider,
    IAgentActionLogRepository actionLogRepository,
    IAuditRepository auditRepository,
    IIntentClassifier intentClassifier,
    IProviderRequirementResolver providerRequirementResolver,
    IChatCompletionProvider chatCompletionProvider,
    IAiOutputValidator aiOutputValidator,
    INotificationDraftService notificationDraftService,
    IAgentGate agentGate,
    IAgentRunRecorder agentRunRecorder,
    Agents.Tools.IAgentToolRegistry agentToolRegistry,
    ICommandCenterActionService? actionService = null) : ICommandCenterQueryService
{
    private const string ModelProvider = "Deterministic";
    private const string PromptVersion = "command-center-query-v1";
    private const string PolicyAnswerPromptVersion = "command-center-policy-answer-v1";
    private const string Visibility = "Internal";
    private const string ConversationalPromptVersion = "command-center-conversational-v1";
    private const string NoModelProvider = "None";

    // Deliberately plain. Every item is something the intent classifier can genuinely route, and
    // is phrased the way a claims officer would say it rather than the way the system stores it.
    private const string GreetingReply =
        "Hello. I can help you with the claims work in this workspace.\n\n"
        + "Try asking me:\n"
        + "- What needs my attention today?\n"
        + "- Which claims are still waiting on documents?\n"
        + "- What is waiting for my approval?\n"
        + "- Which claims are running late?\n"
        + "- What does the policy say about flood damage?\n\n"
        + "For anything about cover, I answer only from your approved documents and show you where "
        + "each answer came from. Decisions stay with you.";

    private const string CapabilitiesReply =
        "I help you keep on top of claims in this workspace. Here is what I can do.\n\n"
        + "**Find work that needs you**\n"
        + "- What needs my attention today?\n"
        + "- Which claims are running late?\n"
        + "- Which claims are still waiting on documents?\n"
        + "- What is waiting for my approval?\n\n"
        + "**Answer questions about cover**\n"
        + "- What does the policy say about flood damage?\n"
        + "- Is there a waiting period on this benefit?\n\n"
        + "Answers about cover come only from your approved documents, with a link to the source "
        + "for each one. If the documents do not say, I tell you that rather than guessing.\n\n"
        + "**Prepare things for you to check**\n"
        + "- Draft an update for a customer\n"
        + "- Put together a summary for management\n\n"
        + "I prepare the work and you decide. I never approve, reject or settle a claim, and "
        + "nothing goes to a customer until a person has approved it.";

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

        // WO-09: every tool call this request makes goes through the registry and lands here,
        // so the AgentRun recorded below carries a real tool trace instead of an empty one.
        var toolTrace = new List<AgentRunToolTraceEntry>();

        // WO-07: every AI execution, completed or refused, is reconstructable as a single
        // AgentRun (docs §11). Shares scope/correlation/user across all branches below; only
        // the agent key, outcome, and trigger differ per branch.
        Task RecordRun(string agentKey, AgentRunStatus status, string triggerType, string? errorCategory = null) =>
            agentRunRecorder.RecordAsync(
                context.Scope,
                agentKey,
                triggerType,
                [SummarizeInput(question)],
                status,
                now,
                correlationId,
                context.UserId,
                caseId: null,
                errorCategory,
                toolTrace: toolTrace,
                cancellationToken: cancellationToken);

        var providerGate = await EnsureRequiredProvidersAsync(intent.Intent, context, toolTrace, cancellationToken);
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

        if (intent.Intent == IntentType.Conversational)
        {
            return await BuildConversationalResponseAsync(
                question,
                context,
                intent.Parameters.GetStringOrDefault(ConversationalKind.Key),
                correlationId,
                now,
                cancellationToken);
        }

        if (actionService is not null
            && intent.Intent is IntentType.DraftCustomerMessage or IntentType.AssignClaim or IntentType.SummarizeClaim)
        {
            return await actionService.HandleAsync(question, intent, context, correlationId, now, cancellationToken);
        }

        if (intent.Intent == IntentType.PrepareCustomerUpdateDraft)
        {
            var draftGate = await agentGate.CheckAsync("customer-update-drafting", cancellationToken);
            if (!draftGate.Enabled)
            {
                await RecordRun("customer-update-drafting", AgentRunStatus.Refused, "command_center_query", "agent_disabled");
                return await BuildAgentDisabledResponseAsync(question, context, draftGate, correlationId, now, cancellationToken);
            }

            var draftResponse = await BuildCustomerUpdateDraftResponseAsync(
                question,
                context,
                intent.Parameters,
                correlationId,
                now,
                cancellationToken);
            await RecordRun("customer-update-drafting", AgentRunStatus.Completed, "command_center_query");
            return draftResponse;
        }

        if (intent.Intent == IntentType.AnswerPolicyQuestion)
        {
            var policyGate = await agentGate.CheckAsync("policy-intelligence", cancellationToken);
            if (!policyGate.Enabled)
            {
                await RecordRun("policy-intelligence", AgentRunStatus.Refused, "command_center_query", "agent_disabled");
                return await BuildAgentDisabledResponseAsync(question, context, policyGate, correlationId, now, cancellationToken);
            }

            var policyResponse = await BuildPolicyAnswerResponseAsync(
                question,
                context,
                correlationId,
                now,
                toolTrace,
                cancellationToken);
            await RecordRun("policy-intelligence", AgentRunStatus.Completed, "command_center_query");
            return policyResponse;
        }

        var scope = context.Scope;
        var data = await LoadQueryDataAsync(scope, context, now, toolTrace, cancellationToken);

        if (intent.Intent == IntentType.GenerateManagementSummary)
        {
            var reportGate = await agentGate.CheckAsync("report-insight", cancellationToken);
            if (!reportGate.Enabled)
            {
                await RecordRun("report-insight", AgentRunStatus.Refused, "command_center_query", "agent_disabled");
                return await BuildAgentDisabledResponseAsync(question, context, reportGate, correlationId, now, cancellationToken);
            }

            var summaryResponse = await BuildManagementSummaryResponseAsync(
                question,
                context,
                intent.Parameters,
                data,
                correlationId,
                now,
                cancellationToken);
            await RecordRun("report-insight", AgentRunStatus.Completed, "command_center_query");
            return summaryResponse;
        }

        var commandCenterGate = await agentGate.CheckAsync("command-center", cancellationToken);
        if (!commandCenterGate.Enabled)
        {
            await RecordRun("command-center", AgentRunStatus.Refused, "command_center_query", "agent_disabled");
            return await BuildAgentDisabledResponseAsync(question, context, commandCenterGate, correlationId, now, cancellationToken);
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
        await RecordRun("command-center", AgentRunStatus.Completed, "command_center_query");

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

    /// <summary>
    /// WO-09: data access now goes through the WO-08 tool registry (claims.attention.read,
    /// approvals.queue.read, metrics.read) instead of calling repositories directly. Every
    /// downstream shape (CommandCenterCaseView, CommandCenterApprovalView, CommandCenterQueryData)
    /// is unchanged, only how the data is fetched changed, not the response.
    /// </summary>
    private async Task<CommandCenterQueryData> LoadQueryDataAsync(
        Domain.Common.TenantScope scope,
        TenantContext context,
        DateTimeOffset now,
        List<AgentRunToolTraceEntry> toolTrace,
        CancellationToken cancellationToken)
    {
        var metrics = await LoadMetricsAsync(scope, context, toolTrace, cancellationToken);

        var snapshots = await CallToolAsync<IReadOnlyList<Agents.Tools.ClaimSnapshot>>(
            "claims.attention.read", scope, context, toolTrace, parameters: null, cancellationToken)
            ?? Array.Empty<Agents.Tools.ClaimSnapshot>();

        var caseViews = new List<CommandCenterCaseView>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            var (token, dueLabel) = CommandCenterQueryPlanner.ResolveSla(snapshot.SlaDueAt, snapshot.SlaStatus, now);

            caseViews.Add(new CommandCenterCaseView(
                snapshot.CaseId,
                snapshot.CustomerName,
                snapshot.ProductLine,
                snapshot.Status,
                snapshot.SlaStatus,
                token,
                dueLabel,
                snapshot.CompletenessScore,
                snapshot.AssignedTo,
                snapshot.MissingDocuments.ToList()));
        }

        var pendingApprovalRequests = await CallToolAsync<IReadOnlyList<ApprovalRequest>>(
            "approvals.queue.read", scope, context, toolTrace, parameters: null, cancellationToken)
            ?? Array.Empty<ApprovalRequest>();
        var pendingApprovals = pendingApprovalRequests
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

    /// <summary>Calls a read-tier tool as the command-center agent and appends its outcome to the
    /// run's tool trace. Returns null (never throws) if the tool was denied, callers already
    /// treat "no data" as the honest empty case, matching AGENTS.md I9.</summary>
    private async Task<T?> CallToolAsync<T>(
        string toolName,
        Domain.Common.TenantScope scope,
        TenantContext context,
        List<AgentRunToolTraceEntry> toolTrace,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
        where T : class
    {
        var result = await agentToolRegistry.ExecuteAsync(
            toolName,
            new Agents.Tools.AgentToolInvocation(scope, "command-center", context.UserId, parameters),
            cancellationToken);

        toolTrace.Add(new AgentRunToolTraceEntry(
            toolName,
            DateTimeOffset.UtcNow,
            result.Success ? "ok" : "denied"));

        return result.Success ? result.Data as T : null;
    }

    private async Task<CommandCenterMetricsResponse> LoadMetricsAsync(
        Domain.Common.TenantScope scope,
        TenantContext context,
        List<AgentRunToolTraceEntry> toolTrace,
        CancellationToken cancellationToken)
    {
        var metrics = await CallToolAsync<CommandCenterMetricsResponse>(
            "metrics.read", scope, context, toolTrace, parameters: null, cancellationToken);
        if (metrics is not null)
        {
            return metrics;
        }

        // Honest fallback if metrics.read is denied for this caller (should not happen in
        // practice, the command-center agent always holds ViewCommandCenter, the same
        // permission the outer QueryAsync call already required): zeroed, not fabricated.
        return new CommandCenterMetricsResponse(0, 0, 0m, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Answers a policy-knowledge question from approved, indexed knowledge only. Retrieval
    /// runs first and the model is given exactly those excerpts; if retrieval returns nothing
    /// the model is never called and the user is told the knowledge base has no approved
    /// source, the product forbids answering policy questions without grounding, so an
    /// honest "not covered by approved sources" is the required behaviour, not a guess.
    /// </summary>
    private async Task<CommandCenterQueryResponse> BuildPolicyAnswerResponseAsync(
        string question,
        TenantContext context,
        string correlationId,
        DateTimeOffset now,
        List<AgentRunToolTraceEntry> toolTrace,
        CancellationToken cancellationToken)
    {
        var scope = context.Scope;

        // WO-09: routed through knowledge.search (WO-08) instead of calling IRetrievalProvider
        // directly. Audience is not a retrieval input, no provider implementation branches on
        // it, so passing it through the tool's fixed "InternalUser" default does not change
        // retrieval behaviour; only .Chunks/.Citations are consumed below, both preserved exactly.
        var retrieval = await CallToolAsync<RetrievalResult>(
            "knowledge.search",
            scope,
            context,
            toolTrace,
            new Dictionary<string, string> { ["query"] = question },
            cancellationToken)
            ?? new RetrievalResult(question, ProductLine: null, Array.Empty<KnowledgeChunk>(), Array.Empty<SourceCitation>());

        if (retrieval.Chunks.Count == 0)
        {
            return await BuildUngroundedPolicyAnswerAsync(question, context, correlationId, now, cancellationToken);
        }

        var sources = string.Join("\n\n", retrieval.Chunks.Select((chunk, index) =>
            $"[{index + 1}] {chunk.DocumentName} (v{chunk.Version}, {chunk.ProductLine}):\n{chunk.Content}"));

        var prompt = string.Join('\n', [
            "You are an insurance operations assistant for staff at a Ugandan composite insurer.",
            "Answer the staff question using ONLY the approved policy sources provided below.",
            "Cite the sources you used by their bracketed number, e.g. [1].",
            "If the sources do not contain the answer, say so plainly instead of guessing.",
            "Do not approve, deny, settle, or repudiate any claim. Do not promise payment or admit liability.",
            "Be concise and factual. Amounts are Ugandan Shillings (UGX).",
            string.Empty,
            $"STAFF QUESTION: {question}",
            string.Empty,
            "APPROVED POLICY SOURCES:",
            sources
        ]);

        var chatResult = await chatCompletionProvider.CompleteAsync(
            new ChatCompletionRequest(prompt, context.TenantId, context.WorkspaceId, correlationId, "policy-intelligence"),
            cancellationToken);

        var blocks = new List<CommandCenterResponseBlock>
        {
            new TextBlock(chatResult.Content),
            new EvidenceListBlock(retrieval.Citations
                .Select(citation => new EvidenceSource(citation.Label, citation.DocumentName, citation.ChunkId))
                .ToArray())
        };

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = null,
            UserId = context.UserId,
            AgentName = "CommandCenterPolicyQnA",
            AssistantMode = AssistantMode.PolicyQnA,
            InputSummary = SummarizeInput(question),
            RetrievedSources = retrieval.Citations.ToArray(),
            ModelOutput = chatResult.Content,
            ConfidenceScore = AiConfidenceScore.Create(0.75m),
            RiskFlag = "none",
            HumanOverride = false,
            CreatedAt = now,
            CorrelationId = correlationId,
            ModelProvider = chatResult.Provider,
            ModelDeployment = chatResult.Deployment,
            PromptVersion = PolicyAnswerPromptVersion,
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
            PolicyAnswerPromptVersion,
            Visibility,
            CanTriggerDecision: false,
            now);
    }

    /// <summary>
    /// Retrieval found no approved source for the question. Says so honestly rather than
    /// letting the model answer a policy question ungrounded.
    /// </summary>
    private async Task<CommandCenterQueryResponse> BuildUngroundedPolicyAnswerAsync(
        string question,
        TenantContext context,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var blocks = new CommandCenterResponseBlock[]
        {
            new TextBlock(
                "I could not find an approved knowledge source covering that question, so I have not answered it. "
                + "IIP only answers policy questions from approved, indexed policy documents. "
                + "A Knowledge Manager can upload and approve the relevant policy wording in Knowledge Base, "
                + "after which this question can be answered with citations.")
        };

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = context.Scope,
            CaseId = null,
            UserId = context.UserId,
            AgentName = "CommandCenterPolicyQnA",
            AssistantMode = AssistantMode.PolicyQnA,
            InputSummary = SummarizeInput(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = "No approved knowledge source matched the question; no answer generated.",
            ConfidenceScore = AiConfidenceScore.Create(0m),
            RiskFlag = "none",
            HumanOverride = false,
            CreatedAt = now,
            CorrelationId = correlationId,
            ModelProvider = ModelProvider,
            ModelDeployment = null,
            PromptVersion = PolicyAnswerPromptVersion,
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
            PolicyAnswerPromptVersion,
            Visibility,
            CanTriggerDecision: false,
            now);
    }

    /// <summary>
    /// Answers a greeting, a thank-you, a goodbye or "what can you do?" in kind.
    ///
    /// This exists because the alternative was worse than unfriendly: "hey" classified as
    /// Unsupported and fell through to the standing operational briefing, so the first thing a
    /// new user saw in reply to a greeting was a count of claims they had not asked about. An
    /// assistant that cannot recognise hello does not read as rigorous, it reads as broken, and
    /// the people being demonstrated to are not the people who built it.
    ///
    /// Fixed text, no model call, no retrieval, no claim data -- so there is nothing here to
    /// ground and nothing to get wrong. The capability list is written in the language of the
    /// job ("waiting for my approval"), not the system ("pending approval queue"), and promises
    /// only what the classifier above can actually route.
    /// </summary>
    private async Task<CommandCenterQueryResponse> BuildConversationalResponseAsync(
        string question,
        TenantContext context,
        string? kind,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reply = kind switch
        {
            ConversationalKind.Thanks => "You are welcome. Ask me anything else when you need it.",
            ConversationalKind.Farewell => "Right you are. I will be here when you need me.",
            ConversationalKind.Capabilities => CapabilitiesReply,
            _ => GreetingReply,
        };

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = context.Scope,
            CaseId = null,
            UserId = context.UserId,
            AgentName = "CommandCenterConversational",
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = SummarizeInput(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = reply,
            ConfidenceScore = AiConfidenceScore.Create(1.0m),
            RiskFlag = "none",
            HumanOverride = false,
            CreatedAt = now,
            CorrelationId = correlationId,
            // No model ran. Recording a provider here would misrepresent a fixed string as
            // generated output in the one place an auditor goes to check exactly that.
            ModelProvider = NoModelProvider,
            ModelDeployment = null,
            PromptVersion = ConversationalPromptVersion,
            Visibility = Visibility,
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);

        return new CommandCenterQueryResponse(
            [new TextBlock(reply)],
            actionLog.AgentActionLogId,
            correlationId,
            NoModelProvider,
            ConversationalPromptVersion,
            Visibility,
            CanTriggerDecision: false,
            now);
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
            // Saying hello reads no claim data, so it requires no claims permission. ViewCommandCenter,
            // already required before this runs, is the whole gate.
            case IntentType.Conversational:
                break;
            case IntentType.AnswerPolicyQuestion:
                await permissionService.RequirePermissionAsync(Permission.ViewAiOutputs, cancellationToken);
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
        TenantContext context,
        List<AgentRunToolTraceEntry> toolTrace,
        CancellationToken cancellationToken)
    {
        foreach (var provider in providerRequirementResolver.GetRequiredProviders(intent))
        {
            // WO-09: routed through provider_health.read (WO-08). Permission is already
            // guaranteed (ViewCommandCenter, required at the top of QueryAsync before this
            // runs), so a null here only ever means "provider not ready", never "denied".
            var health = await CallToolAsync<ProviderStatus>(
                "provider_health.read",
                context.Scope,
                context,
                toolTrace,
                new Dictionary<string, string> { ["providerCode"] = provider.ProviderCode },
                cancellationToken);

            if (health is null || !health.Status.Equals("ready", StringComparison.OrdinalIgnoreCase))
            {
                return new ProviderGateResult(
                    provider.DisplayName,
                    $"{provider.DisplayName} is not configured or not ready.",
                    string.IsNullOrWhiteSpace(health?.Detail) ? "Configure the provider in Admin > Integrations." : health.Detail);
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

    /// <summary>
    /// The kill-switch refusal path (WO-06). Reuses the existing safe-error block/audit
    /// machinery, same as BuildErrorResponseAsync, rather than adding a new response shape,
    /// since the frontend's ErrorBlock already renders a distinguishable, honest refusal with a
    /// correlation reference and no risk of falling through to the "unsupported block" default.
    /// </summary>
    private Task<CommandCenterQueryResponse> BuildAgentDisabledResponseAsync(
        string question,
        TenantContext context,
        AgentGateResult gate,
        string correlationId,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken) =>
        BuildErrorResponseAsync(
            question,
            context,
            "agent_disabled",
            gate.RefusalMessage,
            correlationId,
            generatedAt,
            cancellationToken);

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
            new ChatCompletionRequest(prompt, context.TenantId, context.WorkspaceId, correlationId, "customer-update-drafting"),
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
            new ChatCompletionRequest(prompt, context.TenantId, context.WorkspaceId, correlationId, "report-insight"),
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
