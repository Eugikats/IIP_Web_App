using InsuranceIntelligence.Application.Core;

namespace InsuranceIntelligence.Application.Agents.Tools;

/// <summary>
/// Shared permission and provider-health enforcement so individual tools stay small: each one
/// implements only ExecuteDataAsync, the actual query. Fails closed on both permission denial and
/// an unconfigured/unhealthy required provider (AGENTS.md I9) — never throws past the registry
/// boundary, always returns a structured, honest AgentToolResult.
/// </summary>
public abstract class AgentToolBase(IPermissionService permissionService, IProviderHealthCheck? providerHealthCheck = null) : IAgentTool
{
    public abstract AgentToolDescriptor Descriptor { get; }

    public async Task<AgentToolResult> ExecuteAsync(AgentToolInvocation invocation, CancellationToken cancellationToken)
    {
        var allowed = false;
        foreach (var permission in Descriptor.RequiredPermissionsAny)
        {
            if (await permissionService.HasPermissionAsync(permission, cancellationToken))
            {
                allowed = true;
                break;
            }
        }

        if (!allowed)
        {
            return Denied($"Requires one of: {string.Join(", ", Descriptor.RequiredPermissionsAny)}.");
        }

        if (Descriptor.RequiredProviderCode is not null && providerHealthCheck is not null)
        {
            var health = await providerHealthCheck.CheckAsync(Descriptor.RequiredProviderCode, cancellationToken);
            if (!health.Status.Equals("ready", StringComparison.OrdinalIgnoreCase))
            {
                return new AgentToolResult(
                    Descriptor.Name,
                    Success: false,
                    Data: null,
                    EvidenceRefs: [],
                    DeniedReason: $"{Descriptor.RequiredProviderCode} is not configured or not ready.");
            }
        }

        return await ExecuteDataAsync(invocation, cancellationToken);
    }

    protected abstract Task<AgentToolResult> ExecuteDataAsync(AgentToolInvocation invocation, CancellationToken cancellationToken);

    protected AgentToolResult Ok(object? data, IReadOnlyCollection<string> evidenceRefs) =>
        new(Descriptor.Name, Success: true, data, evidenceRefs, DeniedReason: null);

    protected AgentToolResult Denied(string reason) =>
        new(Descriptor.Name, Success: false, Data: null, EvidenceRefs: [], DeniedReason: reason);
}
