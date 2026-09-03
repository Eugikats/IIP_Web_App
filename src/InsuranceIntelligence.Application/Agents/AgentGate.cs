using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Agents;

namespace InsuranceIntelligence.Application.Agents;

/// <summary>
/// The single enforcement seam for the agent kill switch (ADR-0002; docs/03-design-architecture/
/// iip_ai_agent_architecture_tool_registry_updated.md §11). Every code path that is about to
/// invoke a model or a dedicated AI provider on behalf of one of the ten catalogued agents must
/// call CheckAsync first and refuse — honestly, with who disabled it and when — rather than
/// proceed. Before this type existed, disabling an agent in Admin > Agents wrote a database row
/// that nothing ever read; the switch governed nothing.
///
/// Deliberately separate from IAgentRegistryService: that service requires Permission.ManageAgents
/// because it is the admin management surface. This is a lightweight, permission-free read used
/// internally by every caller who is otherwise already authorized to invoke the agent — it must
/// not itself become a second, differently-permissioned gate.
/// </summary>
public interface IAgentGate
{
    Task<AgentGateResult> CheckAsync(string agentKey, CancellationToken cancellationToken = default);
}

public sealed record AgentGateResult(
    bool Enabled,
    string AgentKey,
    string AgentName,
    string? DisabledByUserId,
    DateTimeOffset? DisabledAt,
    string? Reason)
{
    public string RefusalMessage =>
        DisabledByUserId is not null && DisabledAt is not null
            ? $"The {AgentName} is disabled for this workspace (by {DisabledByUserId} on {DisabledAt:yyyy-MM-dd}{(string.IsNullOrWhiteSpace(Reason) ? "" : $": {Reason}")}). A workspace administrator can re-enable it in Admin > Agents."
            : $"The {AgentName} is disabled for this workspace. A workspace administrator can re-enable it in Admin > Agents.";
}

public sealed class AgentGate(
    ITenantContextAccessor tenantContextAccessor,
    IAgentStateRepository stateRepository) : IAgentGate
{
    public async Task<AgentGateResult> CheckAsync(string agentKey, CancellationToken cancellationToken = default)
    {
        var descriptor = AgentCatalog.Find(agentKey)
            ?? throw new ArgumentException($"Unknown agent key: {agentKey}", nameof(agentKey));

        var context = tenantContextAccessor.Current;
        var states = await stateRepository.ListStatesAsync(context.TenantId, context.WorkspaceId, cancellationToken);
        var state = states.FirstOrDefault(s => string.Equals(s.AgentKey, agentKey, StringComparison.OrdinalIgnoreCase));

        // Absence means "enabled by default" — the same convention AgentRegistryMerge uses for
        // the admin registry listing, kept identical here so the two never disagree.
        return new AgentGateResult(
            state?.Enabled ?? true,
            descriptor.Key,
            descriptor.Name,
            state?.UpdatedByUserId,
            state?.UpdatedAt,
            state?.Reason);
    }
}
