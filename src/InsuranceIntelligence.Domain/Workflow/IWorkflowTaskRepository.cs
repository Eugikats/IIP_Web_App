using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

public interface IWorkflowTaskRepository
{
    Task SaveAsync(WorkflowTask task, CancellationToken cancellationToken = default);

    Task<WorkflowTask?> GetAsync(Guid taskId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WorkflowTask>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WorkflowTask>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
