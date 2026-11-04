using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryAgentActionLogRepository : IAgentActionLogRepository
{
    private readonly ConcurrentBag<AgentActionLog> actionLogs = [];

    public Task SaveAsync(AgentActionLog actionLog, CancellationToken cancellationToken = default)
    {
        actionLogs.Add(actionLog);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<AgentActionLog>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = actionLogs
            .Where(actionLog => actionLog.Scope.Matches(scope) && actionLog.RecordKind != ActionLogKinds.ModelCall)
            .OrderByDescending(actionLog => actionLog.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AgentActionLog>>(matches);
    }

    public Task<IReadOnlyCollection<AgentActionLog>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = actionLogs
            .Where(actionLog => actionLog.CaseId == caseId && actionLog.Scope.Matches(scope) && actionLog.RecordKind != ActionLogKinds.ModelCall)
            .OrderByDescending(actionLog => actionLog.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AgentActionLog>>(matches);
    }
}
