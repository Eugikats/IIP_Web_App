using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

public sealed class InMemoryWorkflowTaskRepository : IWorkflowTaskRepository
{
    private readonly ConcurrentDictionary<Guid, WorkflowTask> tasks = new();

    public Task SaveAsync(WorkflowTask task, CancellationToken cancellationToken = default)
    {
        tasks[task.TaskId] = task;
        return Task.CompletedTask;
    }

    public Task<WorkflowTask?> GetAsync(Guid taskId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task is not null && task.Scope.Matches(scope) ? task : null);
    }

    public Task<IReadOnlyCollection<WorkflowTask>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = tasks.Values
            .Where(task => task.Scope.Matches(scope))
            .OrderByDescending(task => task.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<WorkflowTask>>(matches);
    }

    public Task<IReadOnlyCollection<WorkflowTask>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = tasks.Values
            .Where(task => task.CaseId == caseId && task.Scope.Matches(scope))
            .OrderByDescending(task => task.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<WorkflowTask>>(matches);
    }
}
