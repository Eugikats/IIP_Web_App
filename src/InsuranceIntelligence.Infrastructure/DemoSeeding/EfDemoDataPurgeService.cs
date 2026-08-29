using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.DemoSeeding;

public sealed class EfDemoDataPurgeService(InsuranceIntelligenceDbContext dbContext) : IDemoDataPurgeService
{
    public async Task<int> PurgeCasesAsync(
        IReadOnlyCollection<Guid> caseIds,
        TenantScope scope,
        CancellationToken cancellationToken = default)
    {
        if (caseIds.Count == 0)
        {
            return 0;
        }

        var idSet = caseIds.ToHashSet();

        var cases = await dbContext.Cases
            .Where(c => idSet.Contains(c.Id) && c.TenantId == scope.TenantId && c.WorkspaceId == scope.WorkspaceId)
            .ToListAsync(cancellationToken);

        if (cases.Count == 0)
        {
            return 0;
        }

        var removedCaseIds = cases.Select(c => c.Id).ToHashSet();

        var documents = await dbContext.CaseDocuments
            .Where(d => removedCaseIds.Contains(d.CaseId))
            .ToListAsync(cancellationToken);
        var approvals = await dbContext.Approvals
            .Where(a => removedCaseIds.Contains(a.CaseId))
            .ToListAsync(cancellationToken);
        var workflowTasks = await dbContext.WorkflowTasks
            .Where(w => removedCaseIds.Contains(w.CaseId))
            .ToListAsync(cancellationToken);
        var customerUpdates = await dbContext.CustomerUpdates
            .Where(u => removedCaseIds.Contains(u.CaseId))
            .ToListAsync(cancellationToken);
        var claimReviewRequests = await dbContext.ClaimReviewRequests
            .Where(r => removedCaseIds.Contains(r.CaseId))
            .ToListAsync(cancellationToken);
        var communications = await dbContext.Communications
            .Where(c => removedCaseIds.Contains(c.CaseId))
            .ToListAsync(cancellationToken);
        var auditEvents = await dbContext.AuditEvents
            .Where(a => a.CaseId != null && removedCaseIds.Contains(a.CaseId.Value))
            .ToListAsync(cancellationToken);
        var agentActionLogs = await dbContext.AgentActionLogs
            .Where(a => a.CaseId != null && removedCaseIds.Contains(a.CaseId.Value))
            .ToListAsync(cancellationToken);
        var agentWorkflowRuns = await dbContext.AgentWorkflowRuns
            .Where(r => r.TenantId == scope.TenantId && r.WorkspaceId == scope.WorkspaceId
                && removedCaseIds.Select(id => id.ToString()).Contains(r.BusinessReferenceId))
            .ToListAsync(cancellationToken);
        var agentWorkflowRunIds = agentWorkflowRuns.Select(r => r.Id).ToHashSet();
        var agentWorkItems = agentWorkflowRunIds.Count == 0
            ? []
            : await dbContext.AgentWorkItems
                .Where(w => agentWorkflowRunIds.Contains(w.RunId))
                .ToListAsync(cancellationToken);

        dbContext.CaseDocuments.RemoveRange(documents);
        dbContext.Approvals.RemoveRange(approvals);
        dbContext.WorkflowTasks.RemoveRange(workflowTasks);
        dbContext.CustomerUpdates.RemoveRange(customerUpdates);
        dbContext.ClaimReviewRequests.RemoveRange(claimReviewRequests);
        dbContext.Communications.RemoveRange(communications);
        dbContext.AuditEvents.RemoveRange(auditEvents);
        dbContext.AgentActionLogs.RemoveRange(agentActionLogs);
        dbContext.AgentWorkItems.RemoveRange(agentWorkItems);
        dbContext.AgentWorkflowRuns.RemoveRange(agentWorkflowRuns);
        dbContext.Cases.RemoveRange(cases);

        await dbContext.SaveChangesAsync(cancellationToken);

        return cases.Count;
    }
}
