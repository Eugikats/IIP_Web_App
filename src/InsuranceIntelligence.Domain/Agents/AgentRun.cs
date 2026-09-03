using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Agents;

/// <summary>
/// Every AI execution is reconstructable: which agent, which tools, what inputs, whose
/// authority, what outcome (docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_
/// updated.md §11). Before this type existed, agent activity was recorded only as whatever
/// AuditEvent a given call site happened to write — there was no single, queryable record
/// answering "what did this agent do, with what tools, on whose behalf."
///
/// AgentKey must be a real InsuranceIntelligence.Domain.Agents.AgentCatalog key, not a free-text
/// display name — that is what lets a run be correlated back to the agent's kill-switch state
/// and catalog entry. Never store prompts, secrets, or raw provider payloads here (AGENTS.md I8);
/// InputRefs and the tool trace carry references and safe metadata only.
/// </summary>
public sealed record AgentRun(
    Guid AgentRunId,
    TenantScope Scope,
    string AgentKey,
    string TriggerType,
    IReadOnlyCollection<string> InputRefs,
    AgentRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorCategory,
    string CorrelationId,
    string? UserId,
    Guid? CaseId,
    IReadOnlyCollection<AgentRunToolTraceEntry> ToolTrace);

public enum AgentRunStatus
{
    Completed,
    Refused,
    Failed,
}

/// <summary>
/// One tool invocation within a run. Populated meaningfully once the tool registry exists
/// (docs §7-9); until then a run carries an empty trace rather than a fabricated one.
/// </summary>
public sealed record AgentRunToolTraceEntry(
    string ToolName,
    DateTimeOffset InvokedAt,
    string Outcome);

public interface IAgentRunRepository
{
    Task SaveAsync(AgentRun run, CancellationToken cancellationToken = default);

    Task<AgentRun?> GetAsync(Guid agentRunId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgentRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgentRun>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
