using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Application.Agents;

/// <summary>
/// AI Agent Registry control plane. Merges the code-defined agent catalog with
/// the tenant's persisted kill-switch state, and applies governed, audited
/// kill-switch changes. Never grants an agent authority over a consequential
/// decision (ADR-0002); the kill switch can only ever tighten what runs.
/// </summary>
public sealed class AgentRegistryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAgentStateRepository repository,
    IAuditWriter auditWriter,
    IOperationalTelemetry operationalTelemetry) : IAgentRegistryService
{
    public async Task<IReadOnlyList<AgentRegistryEntryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageAgents, cancellationToken);

        var context = tenantContextAccessor.Current;
        var states = await repository.ListStatesAsync(context.TenantId, context.WorkspaceId, cancellationToken);
        var merged = AgentRegistryMerge.Merge(states.Select(ToDomain).ToArray());
        return merged.Select(ToDto).ToArray();
    }

    public async Task<AgentRegistryEntryDto> SetKillSwitchAsync(
        string agentKey,
        bool enabled,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageAgents, cancellationToken);

        var descriptorForValidation = AgentCatalog.Find(agentKey);
        if (string.IsNullOrWhiteSpace(agentKey) || descriptorForValidation is null)
        {
            throw new AgentRegistryValidationException(nameof(agentKey), "Unknown agent key.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AgentRegistryValidationException(nameof(reason), "A reason is required for a kill-switch change.");
        }

        // WO-11: an agent with no real implementation cannot be enabled, the admin surface must
        // not offer a switch that appears to control something that does not exist.
        if (enabled && descriptorForValidation.ImplementationKind == AgentImplementationKind.NotImplemented)
        {
            throw new AgentRegistryValidationException(nameof(enabled), $"\"{descriptorForValidation.Name}\" has no implementation and cannot be enabled.");
        }

        var context = tenantContextAccessor.Current;
        var now = DateTimeOffset.UtcNow;
        var state = new AgentKillSwitchStateDto(agentKey, enabled, context.UserId, now, reason.Trim());

        await repository.UpsertStateAsync(context.TenantId, context.WorkspaceId, state, cancellationToken);

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                context.Scope,
                CaseId: null,
                DocumentId: null,
                UserId: context.UserId,
                ActorType.User,
                enabled ? "agent.killswitch.enabled" : "agent.killswitch.disabled",
                now,
                BeforeStatus: null,
                AfterStatus: enabled ? "enabled" : "disabled",
                EvidenceIds: [agentKey],
                ModelProvider: "AgentRegistry",
                Confidence: null,
                ValidationResult: "recorded",
                ApprovalId: null,
                AuditReason: reason.Trim(),
                CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);

        operationalTelemetry.TrackAgentRegistryRun(
            context.TenantId,
            context.WorkspaceId,
            agentKey,
            enabled ? "kill-switch-enabled" : "kill-switch-disabled",
            "recorded");

        var descriptor = AgentCatalog.Find(agentKey)!;
        return ToDto(new AgentRegistryEntry(descriptor, enabled, context.UserId, now, reason.Trim()));
    }

    private static AgentKillSwitchState ToDomain(AgentKillSwitchStateDto dto) =>
        new(dto.AgentKey, dto.Enabled, dto.UpdatedByUserId, dto.UpdatedAt, dto.Reason);

    private static AgentRegistryEntryDto ToDto(AgentRegistryEntry entry) =>
        new(
            entry.Descriptor.Key,
            entry.Descriptor.Name,
            entry.Descriptor.Purpose,
            entry.Descriptor.RiskLevel,
            entry.Descriptor.HumanApprovalRequired,
            entry.Descriptor.ImplementationKind.ToString(),
            entry.Descriptor.Tools,
            // WO-11: an unimplemented agent is never shown enabled, regardless of what stale
            // persisted state says, the switch controls nothing real for it.
            entry.Descriptor.ImplementationKind == AgentImplementationKind.NotImplemented ? false : entry.Enabled,
            entry.UpdatedByUserId,
            entry.UpdatedAt?.ToString("O"),
            entry.Reason);
}
