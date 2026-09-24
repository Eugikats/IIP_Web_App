using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Agents.Tools;

/// <summary>
/// metrics.read, real backend aggregate, tenant/workspace scoped with source labels and a
/// generated timestamp (docs §9). Same aggregation CommandCenterQueryService.LoadMetricsAsync
/// performs today, reused here (not copied) via the same CommandCenterMetricsResponse type so
/// WO-09 can retire the private duplicate once Command Center calls this tool instead.
/// </summary>
public sealed class MetricsReadTool(
    ICaseRepository caseRepository,
    ICaseDocumentRepository caseDocumentRepository,
    IKnowledgeDocumentRepository knowledgeRepository,
    IAgentActionLogRepository actionLogRepository,
    IApprovalRepository approvalRepository,
    IWorkflowTaskRepository taskRepository,
    ICustomerUpdateRepository updateRepository,
    ISlaCalculator slaCalculator,
    IPermissionService permissionService) : AgentToolBase(permissionService)
{
    public override AgentToolDescriptor Descriptor { get; } = new(
        "metrics.read",
        ["command-center", "report-insight"],
        [Permission.ViewCommandCenter],
        RequiredProviderCode: null,
        Guardrail: "Tenant/workspace scoped; real backend values only, no hardcoded briefing.");

    protected override async Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var scope = invocation.Scope;
        var now = DateTimeOffset.UtcNow;
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
        var caseSlaStatuses = cases
            .Select(claimCase => (Case: claimCase, Sla: slaCalculator.Calculate(claimCase, now)))
            .ToArray();
        var today = now.Date;
        var averageCompleteness = cases.Count == 0
            ? 0m
            : Math.Round(cases.Average(claimCase => claimCase.CompletenessScore), 2);

        var response = new CommandCenterMetricsResponse(
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
            caseSlaStatuses.Count(item => item.Sla == SlaStatus.AtRisk && item.Case.Status != CaseStatus.Closed),
            caseSlaStatuses.Count(item => item.Sla == SlaStatus.Breached && item.Case.Status != CaseStatus.Closed),
            caseSlaStatuses.Count(item => item.Sla == SlaStatus.Breached && item.Case.Status == CaseStatus.Closed));

        return Ok(response, [$"tenant:{scope.TenantId}/workspace:{scope.WorkspaceId}/as-of:{now:O}"]);
    }
}
