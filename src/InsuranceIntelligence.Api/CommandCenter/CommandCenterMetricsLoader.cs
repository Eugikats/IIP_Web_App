using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.CommandCenter;

// Single-sourced Command Center metric math + tenant-scope resolution, salvaged from the
// former CommandCenterEndpoints during the v2 migration. Backs both the GET /metrics endpoint
// (metric_pills) and the chat-first /query planner. The superseded prose AskOperationalQuestion
// method was dropped (replaced by /query).
public static class CommandCenterMetricsLoader
{
    public static async Task<IResult> GetMetrics(
        ICaseRepository caseRepository,
        ICaseDocumentRepository caseDocumentRepository,
        IKnowledgeDocumentRepository knowledgeRepository,
        IAgentActionLogRepository actionLogRepository,
        IApprovalRepository approvalRepository,
        IWorkflowTaskRepository taskRepository,
        ICustomerUpdateRepository updateRepository,
        ISlaCalculator slaCalculator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await LoadMetricsAsync(
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

        return result.Error ?? Results.Ok(result.Metrics);
    }

    internal static async Task<MetricsLoadResult> LoadMetricsAsync(
        ICaseRepository caseRepository,
        ICaseDocumentRepository caseDocumentRepository,
        IKnowledgeDocumentRepository knowledgeRepository,
        IAgentActionLogRepository actionLogRepository,
        IApprovalRepository approvalRepository,
        IWorkflowTaskRepository taskRepository,
        ICustomerUpdateRepository updateRepository,
        ISlaCalculator slaCalculator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return new MetricsLoadResult(null, null, Validation("tenantScope", validationError));
        }

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
        var slaStatuses = cases.Select(claimCase => slaCalculator.Calculate(claimCase, DateTimeOffset.UtcNow)).ToArray();
        var today = DateTimeOffset.UtcNow.Date;
        var averageCompleteness = cases.Count == 0
            ? 0m
            : Math.Round(cases.Average(claimCase => claimCase.CompletenessScore), 2);

        var metrics = new CommandCenterMetricsResponse(
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

        return new MetricsLoadResult(scope, metrics, null);
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    internal sealed record MetricsLoadResult(
        TenantScope? Scope,
        CommandCenterMetricsResponse? Metrics,
        IResult? Error);
}
