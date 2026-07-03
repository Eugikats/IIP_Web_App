using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.CommandCenter;

/// <summary>
/// Chat-first Command Center query endpoint: <c>POST /api/v1/command-center/query</c>.
/// Returns a governed, tenant-scoped set of typed answer blocks assembled by a deterministic
/// planner from real data. Preparation only — the envelope's <c>canTriggerDecision</c> is always
/// <c>false</c> and every proposed action requires human approval.
/// </summary>
public static class CommandCenterQueryEndpoints
{
    private const string ModelProvider = "Deterministic";
    private const string PromptVersion = "command-center-query-v1";
    private const string Visibility = "Internal";

    public static async Task<IResult> RunQuery(
        CommandCenterQueryRequest request,
        InfrastructureOptions options,
        ICaseRepository caseRepository,
        ICaseDocumentRepository caseDocumentRepository,
        IDocumentChecklistProvider checklistProvider,
        IKnowledgeDocumentRepository knowledgeRepository,
        IAgentActionLogRepository actionLogRepository,
        IApprovalRepository approvalRepository,
        IWorkflowTaskRepository taskRepository,
        ICustomerUpdateRepository updateRepository,
        IAuditRepository auditRepository,
        ISlaCalculator slaCalculator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["question"] = ["A Command Center question is required."],
            });
        }

        var question = request.Question.Trim();
        if (question.Length > 2000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["question"] = ["A Command Center question must be 2,000 characters or fewer."],
            });
        }

        // Reuse the single-sourced metric math + tenant-scope resolution.
        var loaded = await CommandCenterMetricsLoader.LoadMetricsAsync(
            caseRepository,
            caseDocumentRepository,
            knowledgeRepository,
            actionLogRepository,
            approvalRepository,
            taskRepository,
            updateRepository,
            slaCalculator,
            httpContext,
            cancellationToken);

        if (loaded.Error is not null)
        {
            return loaded.Error;
        }

        var scope = loaded.Scope!;
        var metrics = loaded.Metrics!;
        var now = DateTimeOffset.UtcNow;

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

        var providerHealth = AdminEndpoints.BuildProviderHealth(options);
        var missingProviders = providerHealth.Providers
            .Where(provider => provider.Status == "missing")
            .ToList();

        var data = new CommandCenterQueryData(metrics, caseViews, pendingApprovals, missingProviders);
        var blocks = CommandCenterQueryPlanner.Plan(question, data);

        var correlationId = GetCorrelationId(httpContext);
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = null,
            UserId = DemoAuthorization.GetUserId(httpContext, "runtime-user"),
            AgentName = "CommandCenterQuery",
            AssistantMode = AssistantMode.OperationalSummary,
            InputSummary = SummarizeInput(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = $"Command Center briefing: {blocks.Count} block(s) over {caseViews.Count} claim(s).",
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

        return Results.Ok(new CommandCenterQueryResponse(
            blocks,
            actionLog.AgentActionLogId,
            correlationId,
            ModelProvider,
            PromptVersion,
            Visibility,
            CanTriggerDecision: false,
            now));
    }

    private static string SummarizeInput(string question) =>
        question.Length <= 180 ? question : $"{question[..180]}...";

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
