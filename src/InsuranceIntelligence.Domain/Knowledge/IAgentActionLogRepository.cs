using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public interface IAgentActionLogRepository
{
    Task SaveAsync(AgentActionLog actionLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgentActionLog>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgentActionLog>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
