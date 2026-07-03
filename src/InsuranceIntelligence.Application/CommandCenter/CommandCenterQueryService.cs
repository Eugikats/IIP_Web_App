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
    IAgentActionLogRepository actionLogRepository,
    IApprovalRepository approvalRepository,
    IWorkflowTaskRepository taskRepository,
    ICustomerUpdateRepository updateRepository,
    IAuditRepository auditRepository,
    ISlaCalculator slaCalculator) : ICommandCenterQueryService
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
        var intent = ResolveIntent(question);
        await RequireIntentPermissionAsync(intent, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var scope = context.Scope;
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

        var data = new CommandCenterQueryData(
            metrics,
            caseViews,
            pendingApprovals,
            Array.Empty<CommandCenterProviderStatusItem>());

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

    private async Task RequireIntentPermissionAsync(CommandCenterIntent intent, CancellationToken cancellationToken)
    {
        switch (intent)
        {
            case CommandCenterIntent.Approvals:
                await permissionService.RequirePermissionAsync(Permission.ApproveSettlementWorkflowStep, cancellationToken);
                break;
            case CommandCenterIntent.Claims:
            case CommandCenterIntent.Documents:
            case CommandCenterIntent.Sla:
            case CommandCenterIntent.Default:
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

    private static CommandCenterIntent ResolveIntent(string question)
    {
        var normalized = question.Trim().ToLowerInvariant();

        if (Mentions(normalized, "approval", "approve", "sign off", "sign-off"))
        {
            return CommandCenterIntent.Approvals;
        }

        if (Mentions(normalized, "document", "missing", "incomplete", "checklist", "evidence pack"))
        {
            return CommandCenterIntent.Documents;
        }

        if (Mentions(normalized, "sla", "breach", "overdue", "at risk", "at-risk", "due", "deadline"))
        {
            return CommandCenterIntent.Sla;
        }

        if (Mentions(normalized, "claim", "case", "workload", "queue"))
        {
            return CommandCenterIntent.Claims;
        }

        return CommandCenterIntent.Default;
    }

    private static bool Mentions(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static string SummarizeInput(string question) =>
        question.Length <= 180 ? question : $"{question[..180]}...";

    private enum CommandCenterIntent
    {
        Default,
        Claims,
        Documents,
        Sla,
        Approvals
    }
}

public sealed class CommandCenterValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
