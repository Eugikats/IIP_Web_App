using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Reports;

public static class ReportsEndpoints
{
    public static async Task<IResult> GetOperationsSummary(
        ICaseRepository caseRepository,
        IWorkflowTaskRepository taskRepository,
        IApprovalRepository approvalRepository,
        ICustomerUpdateRepository updateRepository,
        IAgentActionLogRepository actionLogRepository,
        ISlaCalculator slaCalculator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var tasks = await taskRepository.ListAsync(scope, cancellationToken);
        var approvals = await approvalRepository.ListAsync(scope, cancellationToken);
        var updates = await updateRepository.ListAsync(scope, cancellationToken);
        var actionLogs = await actionLogRepository.ListAsync(scope, cancellationToken);
        var today = DateTimeOffset.UtcNow.Date;
        var averageCompleteness = cases.Count == 0 ? 0m : Math.Round(cases.Average(claimCase => claimCase.CompletenessScore), 2);
        var slaStatuses = cases.Select(claimCase => slaCalculator.Calculate(claimCase, DateTimeOffset.UtcNow)).ToArray();

        return Results.Ok(new OperationsSummaryResponse(
            cases.Count(claimCase => claimCase.Status != CaseStatus.Closed),
            cases.Count(claimCase => claimCase.CreatedAt.Date == today),
            cases.Count(claimCase => claimCase.CompletenessScore < 100m),
            averageCompleteness,
            approvals.Count(approval => approval.Status == ApprovalStatus.Pending),
            tasks.Count(task => task.Status is WorkflowTaskStatus.Open or WorkflowTaskStatus.InProgress),
            updates.Count(update => update.Status is CustomerUpdateStatus.Draft or CustomerUpdateStatus.PendingApproval or CustomerUpdateStatus.Approved),
            actionLogs.Count,
            actionLogs.Count(actionLog => actionLog.RiskFlag is "no_source" or "needs_review"),
            slaStatuses.Count(status => status == SlaStatus.AtRisk),
            slaStatuses.Count(status => status == SlaStatus.Breached),
            cases.GroupBy(claimCase => claimCase.Status.ToString()).Select(group => new StatusCountResponse(group.Key, group.Count())).ToArray(),
            tasks.GroupBy(task => task.TaskType.ToString()).Select(group => new StatusCountResponse(group.Key, group.Count())).ToArray()));
    }
}
