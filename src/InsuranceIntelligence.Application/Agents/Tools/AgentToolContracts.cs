using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Agents.Tools;

/// <summary>
/// The tool registry docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md
/// §7-9 calls for. Deliberately NOT a resurrection of the legacy IAgentTool/IAgentToolRegistry in
/// InsuranceIntelligence.Domain.Agents.AgentContracts — that framework was dropped during the v2
/// migration (see the removed-framework note this file's Program.cs registration replaces) and is
/// built around a prose message+module+intent shape from an orchestrator endpoint the deployed API
/// does not expose. This registry is shaped around what CommandCenterQueryService actually needs —
/// TenantScope-scoped, typed, permission-checked read operations — so WO-09 can genuinely route
/// Command Center's data access through it rather than gaining a second, unused implementation.
///
/// Read tier only (side-effect level "read" per doc §8): every tool here is a pure query. Write,
/// draft, external_send, and approval_required tiers are out of scope for this work order.
/// </summary>
public sealed record AgentToolDescriptor(
    string Name,
    IReadOnlyCollection<string> AllowedAgentKeys,
    IReadOnlyCollection<Permission> RequiredPermissionsAny,
    string? RequiredProviderCode,
    string Guardrail);

public sealed record AgentToolInvocation(
    TenantScope Scope,
    string AgentKey,
    string? UserId,
    IReadOnlyDictionary<string, string>? Parameters = null)
{
    public string? Parameter(string key) =>
        Parameters is not null && Parameters.TryGetValue(key, out var value) ? value : null;
}

public sealed record AgentToolResult(
    string ToolName,
    bool Success,
    object? Data,
    IReadOnlyCollection<string> EvidenceRefs,
    string? DeniedReason);

public interface IAgentTool
{
    AgentToolDescriptor Descriptor { get; }

    Task<AgentToolResult> ExecuteAsync(AgentToolInvocation invocation, CancellationToken cancellationToken);
}

public interface IAgentToolRegistry
{
    IReadOnlyCollection<AgentToolDescriptor> Descriptors { get; }

    Task<AgentToolResult> ExecuteAsync(string toolName, AgentToolInvocation invocation, CancellationToken cancellationToken);
}

/// <summary>
/// Central enforcement point: every tool call passes through here for the allowed-agent-key check
/// (§9's "Allowed agents" column) and unknown-tool handling, so no individual tool can skip it.
/// Per-tool permission/provider-health checks stay in AgentToolBase, since they differ per tool.
/// </summary>
public sealed class AgentToolRegistry(IEnumerable<IAgentTool> tools) : IAgentToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> toolsByName =
        tools.ToDictionary(tool => tool.Descriptor.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<AgentToolDescriptor> Descriptors =>
        toolsByName.Values.Select(tool => tool.Descriptor).ToArray();

    public async Task<AgentToolResult> ExecuteAsync(string toolName, AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (!toolsByName.TryGetValue(toolName, out var tool))
        {
            return new AgentToolResult(toolName, Success: false, Data: null, EvidenceRefs: [], DeniedReason: "Unknown tool.");
        }

        if (!tool.Descriptor.AllowedAgentKeys.Contains(invocation.AgentKey, StringComparer.OrdinalIgnoreCase))
        {
            return new AgentToolResult(
                toolName,
                Success: false,
                Data: null,
                EvidenceRefs: [],
                DeniedReason: $"Agent \"{invocation.AgentKey}\" is not permitted to call tool \"{toolName}\".");
        }

        return await tool.ExecuteAsync(invocation, cancellationToken);
    }
}
