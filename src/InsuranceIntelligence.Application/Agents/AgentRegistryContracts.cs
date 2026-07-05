namespace InsuranceIntelligence.Application.Agents;

public interface IAgentRegistryService
{
    Task<IReadOnlyList<AgentRegistryEntryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AgentRegistryEntryDto> SetKillSwitchAsync(
        string agentKey,
        bool enabled,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>Persistence port for per-agent, tenant-scoped kill-switch state.</summary>
public interface IAgentStateRepository
{
    Task<IReadOnlyList<AgentKillSwitchStateDto>> ListStatesAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default);

    Task UpsertStateAsync(
        string tenantId,
        string workspaceId,
        AgentKillSwitchStateDto state,
        CancellationToken cancellationToken = default);
}

public sealed record AgentKillSwitchStateDto(
    string AgentKey,
    bool Enabled,
    string? UpdatedByUserId,
    DateTimeOffset? UpdatedAt,
    string? Reason);

public sealed record AgentRegistryEntryDto(
    string Key,
    string Name,
    string Purpose,
    string RiskLevel,
    bool HumanApprovalRequired,
    bool Enabled,
    string? UpdatedByUserId,
    string? UpdatedAt,
    string? Reason);

public sealed class AgentRegistryValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
