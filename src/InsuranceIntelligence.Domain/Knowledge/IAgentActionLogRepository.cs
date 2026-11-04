using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

/// <summary>Lists return actions only; the bare model-call records written for metering are read through <c>IWorkspaceUsageReader</c>.</summary>
public interface IAgentActionLogRepository
{
    Task SaveAsync(AgentActionLog actionLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgentActionLog>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgentActionLog>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
