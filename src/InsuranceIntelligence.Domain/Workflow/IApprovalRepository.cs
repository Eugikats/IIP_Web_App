using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

public interface IApprovalRepository
{
    Task SaveAsync(ApprovalRequest approval, CancellationToken cancellationToken = default);

    Task<ApprovalRequest?> GetAsync(Guid approvalId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ApprovalRequest>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ApprovalRequest>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
