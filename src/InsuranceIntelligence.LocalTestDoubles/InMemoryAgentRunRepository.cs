using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryAgentRunRepository : IAgentRunRepository
{
    private readonly ConcurrentDictionary<Guid, AgentRun> runs = new();

    public Task SaveAsync(AgentRun run, CancellationToken cancellationToken = default)
    {
        runs[run.AgentRunId] = run;
        return Task.CompletedTask;
    }

    public Task<AgentRun?> GetAsync(Guid agentRunId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var found = runs.TryGetValue(agentRunId, out var run) && run.Scope.Matches(scope) ? run : null;
        return Task.FromResult(found);
    }

    public Task<IReadOnlyCollection<AgentRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = runs.Values
            .Where(run => run.Scope.Matches(scope))
            .OrderByDescending(run => run.StartedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AgentRun>>(matches);
    }

    public Task<IReadOnlyCollection<AgentRun>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = runs.Values
            .Where(run => run.CaseId == caseId && run.Scope.Matches(scope))
            .OrderByDescending(run => run.StartedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AgentRun>>(matches);
    }
}
