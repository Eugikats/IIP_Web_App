using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

public sealed class InMemoryApprovalRepository : IApprovalRepository
{
    private readonly ConcurrentDictionary<Guid, ApprovalRequest> approvals = new();

    public Task SaveAsync(ApprovalRequest approval, CancellationToken cancellationToken = default)
    {
        approvals[approval.ApprovalId] = approval;
        return Task.CompletedTask;
    }

    public Task<ApprovalRequest?> GetAsync(Guid approvalId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        approvals.TryGetValue(approvalId, out var approval);
        return Task.FromResult(approval is not null && approval.Scope.Matches(scope) ? approval : null);
    }

    public Task<IReadOnlyCollection<ApprovalRequest>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = approvals.Values
            .Where(approval => approval.Scope.Matches(scope))
            .OrderByDescending(approval => approval.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ApprovalRequest>>(matches);
    }

    public Task<IReadOnlyCollection<ApprovalRequest>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = approvals.Values
            .Where(approval => approval.CaseId == caseId && approval.Scope.Matches(scope))
            .OrderByDescending(approval => approval.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ApprovalRequest>>(matches);
    }
}
