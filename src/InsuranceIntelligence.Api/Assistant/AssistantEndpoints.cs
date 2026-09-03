using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Assistant;

public static class AssistantEndpoints
{
    public static async Task<IResult> AskPolicyQuestion(
        PolicyQuestionRequest request,
        IRetrievalProvider retrievalProvider,
        IAiAssistantProvider assistantProvider,
        IAgentActionLogRepository actionLogRepository,
        IAuditRepository auditRepository,
        ICaseRepository caseRepository,
        IAgentGate agentGate,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Validation("question", "Policy question is required.");
        }

        var actor = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Validation("userId", "Authenticated user context is required to ask a policy question.");
        }

        if (request.CaseId is not null)
        {
            var claimCase = await caseRepository.GetAsync(request.CaseId.Value, cancellationToken);
            if (claimCase is null
                || !claimCase.Scope.Matches(scope)
                || !ClaimAccessGuard.CanViewCase(httpContext, claimCase))
            {
                return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
            }
        }

        var correlationId = GetCorrelationId(httpContext);
        var question = new AssistantQuestion(
            scope,
            request.CaseId,
            request.Question.Trim(),
            string.IsNullOrWhiteSpace(request.ProductLine) ? null : request.ProductLine.Trim(),
            NormalizeAudience(request.Audience),
            actor,
            correlationId);
        // WO-06: fail closed if a workspace administrator disabled the policy-intelligence agent,
        // before retrieval or the model runs. Mirrors NotConfiguredAiAssistantProvider's shape so
        // the refusal flows through the exact same actionLog/audit/response path as a real answer.
        var gate = await agentGate.CheckAsync("policy-intelligence", cancellationToken);
        var answer = gate.Enabled
            ? await AnswerAsync(retrievalProvider, assistantProvider, question, cancellationToken)
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
                string.Equals(question.Audience, "Customer", StringComparison.OrdinalIgnoreCase) ? "CustomerSafe" : "Internal");
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
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
            CanTriggerDecision = answer.CanTriggerDecision
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.AssistantPolicyQuestionAnswered(actionLog), cancellationToken);

        return Results.Ok(new PolicyQuestionResponse(
            answer.AnswerText,
            answer.ConfidenceScore.Value,
            answer.EscalationRequired,
            answer.GuardrailMessage,
            answer.RiskFlag,
            CoverageState(answer),
            answer.Sources.Select(AssistantMapper.ToResponse).ToArray(),
            actionLog.AgentActionLogId,
            actionLog.ModelProvider,
            actionLog.ModelDeployment,
            actionLog.PromptVersion,
            actionLog.Visibility,
            actionLog.CanTriggerDecision,
            actionLog.CreatedAt,
            actionLog.CorrelationId));
    }

    public static async Task<IResult> ListActions(
        IAgentActionLogRepository actionLogRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var actionLogs = await actionLogRepository.ListAsync(scope, cancellationToken);
        return Results.Ok(actionLogs.Select(AssistantMapper.ToResponse).ToArray());
    }

    public static async Task<IResult> ListActionsForCase(
        Guid caseId,
        ICaseRepository caseRepository,
        IAgentActionLogRepository actionLogRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null
            || !claimCase.Scope.Matches(scope)
            || !ClaimAccessGuard.CanViewCase(httpContext, claimCase))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var actionLogs = await actionLogRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(actionLogs.Select(AssistantMapper.ToResponse).ToArray());
    }

    private static async Task<AssistantAnswer> AnswerAsync(
        IRetrievalProvider retrievalProvider,
        IAiAssistantProvider assistantProvider,
        AssistantQuestion question,
        CancellationToken cancellationToken)
    {
        var retrieval = await retrievalProvider.RetrieveAsync(question, cancellationToken);
        return await assistantProvider.AnswerPolicyQuestionAsync(question, retrieval, cancellationToken);
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static string NormalizeAudience(string? audience) =>
        string.Equals(audience, "Customer", StringComparison.OrdinalIgnoreCase) ? "Customer" : "InternalUser";

    private static string SummarizeInput(string question) =>
        question.Length <= 180 ? question : $"{question[..180]}...";

    private static string CoverageState(AssistantAnswer answer)
    {
        if (string.Equals(answer.RiskFlag, "conflicting_sources", StringComparison.OrdinalIgnoreCase))
        {
            return "ConflictingSources";
        }

        if (answer.Sources.Count == 0
            || string.Equals(answer.RiskFlag, "no_source", StringComparison.OrdinalIgnoreCase))
        {
            return "InsufficientApprovedEvidence";
        }

        return answer.EscalationRequired
            ? "CoverageUnclear"
            : "GroundedAnswerAvailable";
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
}
