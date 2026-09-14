using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class AgentRegistryTests
{
    [Fact]
    public void Merge_NoStates_AllAgentsEnabledByDefault()
    {
        var merged = AgentRegistryMerge.Merge([]);

        Assert.Equal(AgentCatalog.Agents.Count, merged.Count);
        Assert.All(merged, entry => Assert.True(entry.Enabled));
    }

    [Fact]
    public void Merge_DisabledState_OverridesDefault()
    {
        var key = AgentCatalog.Agents[0].Key;
        var merged = AgentRegistryMerge.Merge([new AgentKillSwitchState(key, Enabled: false, "user-1", DateTimeOffset.UtcNow, "maintenance")]);

        var entry = Assert.Single(merged, e => e.Descriptor.Key == key);
        Assert.False(entry.Enabled);
        Assert.Equal("maintenance", entry.Reason);
    }

    [Fact]
    public async Task List_RequiresManageAgentsPermission()
    {
        var service = new AgentRegistryService(
            Context(),
            new DenyPermissionService(),
            new InMemoryAgentStateRepository(),
            new NullAuditWriter(),
            NoOpOperationalTelemetry.Instance);
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.ListAsync());
    }

    [Fact]
    public async Task SetKillSwitch_PersistsStateAndWritesAudit()
    {
        var repo = new InMemoryAgentStateRepository();
        var audit = new NullAuditWriter();
        var telemetry = new RecordingOperationalTelemetry();
        var service = new AgentRegistryService(Context(), new AllowPermissionService(), repo, audit, telemetry);
        var key = AgentCatalog.Agents[0].Key;

        var result = await service.SetKillSwitchAsync(key, enabled: false, "kill switch drill");

        Assert.False(result.Enabled);
        Assert.Single(repo.Saved, s => s.AgentKey == key && !s.Enabled);
        Assert.Single(audit.Events, e => e.ActionName == "agent.killswitch.disabled");
        Assert.Single(telemetry.AgentRuns, run =>
            run.AgentKey == key
            && run.Action == "kill-switch-disabled"
            && run.Result == "recorded"
            && run.TenantId == "tenant-a"
            && run.WorkspaceId == "workspace-a");

        var listed = await service.ListAsync();
        Assert.False(Assert.Single(listed, e => e.Key == key).Enabled);
    }

    [Fact]
    public async Task SetKillSwitch_CannotEnableNotImplementedAgent()
    {
        // WO-11: the admin surface must not offer a switch that appears to control an agent that
        // has no real implementation. knowledge-assistant is the one such agent found by the
        // build's own call-site inventory.
        var service = new AgentRegistryService(
            Context(),
            new AllowPermissionService(),
            new InMemoryAgentStateRepository(),
            new NullAuditWriter(),
            NoOpOperationalTelemetry.Instance);

        var exception = await Assert.ThrowsAsync<AgentRegistryValidationException>(
            () => service.SetKillSwitchAsync("knowledge-assistant", enabled: true, "attempting to enable an unimplemented agent"));

        Assert.Contains("cannot be enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetKillSwitch_CanStillDisableAnAlreadyDisabledNotImplementedAgent()
    {
        // Disabling is always safe, it only ever tightens what runs (ADR-0002).
        var service = new AgentRegistryService(
            Context(),
            new AllowPermissionService(),
            new InMemoryAgentStateRepository(),
            new NullAuditWriter(),
            NoOpOperationalTelemetry.Instance);

        var result = await service.SetKillSwitchAsync("knowledge-assistant", enabled: false, "explicit disable for clarity");

        Assert.False(result.Enabled);
    }

    [Fact]
    public async Task ListAsync_NeverShowsANotImplementedAgentAsEnabled()
    {
        // Even if a stale persisted state somehow says enabled=true (e.g. written before this
        // agent was reclassified), the API-facing DTO must not claim it is running.
        var repo = new InMemoryAgentStateRepository();
        repo.Saved.Add(new AgentKillSwitchStateDto("knowledge-assistant", Enabled: true, "user-1", DateTimeOffset.UtcNow, "stale"));
        var service = new AgentRegistryService(Context(), new AllowPermissionService(), repo, new NullAuditWriter(), NoOpOperationalTelemetry.Instance);

        var listed = await service.ListAsync();

        var entry = Assert.Single(listed, e => e.Key == "knowledge-assistant");
        Assert.False(entry.Enabled);
        Assert.Equal("NotImplemented", entry.ImplementationKind);
    }

    [Fact]
    public void AgentCatalog_EveryEntry_HasToolsConsistentWithImplementationKind()
    {
        // Sanity check on the catalog itself: an agent that calls no AI service or model has no
        // business claiming tools it never invokes, and a real agent should name at least one.
        foreach (var descriptor in AgentCatalog.Agents)
        {
            if (descriptor.ImplementationKind == AgentImplementationKind.NotImplemented)
            {
                Assert.Empty(descriptor.Tools);
            }
            else
            {
                Assert.NotEmpty(descriptor.Tools);
            }
        }
    }

    [Fact]
    public async Task SetKillSwitch_UnknownAgent_Throws()
    {
        var service = new AgentRegistryService(
            Context(),
            new AllowPermissionService(),
            new InMemoryAgentStateRepository(),
            new NullAuditWriter(),
            NoOpOperationalTelemetry.Instance);
        await Assert.ThrowsAsync<AgentRegistryValidationException>(() => service.SetKillSwitchAsync("no-such-agent", true, "x"));
    }

    [Fact]
    public async Task SetKillSwitch_MissingReason_Throws()
    {
        var service = new AgentRegistryService(
            Context(),
            new AllowPermissionService(),
            new InMemoryAgentStateRepository(),
            new NullAuditWriter(),
            NoOpOperationalTelemetry.Instance);
        await Assert.ThrowsAsync<AgentRegistryValidationException>(() => service.SetKillSwitchAsync(AgentCatalog.Agents[0].Key, true, "  "));
    }

    [Fact]
    public async Task KillSwitch_WrittenByAdminService_IsReadByRuntimeGate()
    {
        // WO-06 end-to-end proof: the same write path Admin > Agents uses
        // (AgentRegistryService.SetKillSwitchAsync) against the same persisted state an
        // AgentGate reads at invocation time. Before AgentGate existed, this loop was open,
        // disabling an agent in the admin UI wrote a row that no runtime code ever consulted.
        var repo = new InMemoryAgentStateRepository();
        var registryService = new AgentRegistryService(Context(), new AllowPermissionService(), repo, new NullAuditWriter(), NoOpOperationalTelemetry.Instance);
        var gate = new AgentGate(Context(), repo);
        var key = AgentCatalog.Agents[0].Key;

        var beforeDisable = await gate.CheckAsync(key);
        Assert.True(beforeDisable.Enabled);

        await registryService.SetKillSwitchAsync(key, enabled: false, "verifying end-to-end enforcement");
        var afterDisable = await gate.CheckAsync(key);

        Assert.False(afterDisable.Enabled);
        Assert.Equal("user-1", afterDisable.DisabledByUserId);
        Assert.Equal("verifying end-to-end enforcement", afterDisable.Reason);
        Assert.Contains("disabled for this workspace", afterDisable.RefusalMessage, StringComparison.OrdinalIgnoreCase);

        await registryService.SetKillSwitchAsync(key, enabled: true, "re-enabling after drill");
        var afterReenable = await gate.CheckAsync(key);

        Assert.True(afterReenable.Enabled);
    }

    private static ITenantContextAccessor Context() => new FixedContext();

    private sealed class FixedContext : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-a", "workspace-a", "user-1", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class AllowPermissionService : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class DenyPermissionService : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => throw new PermissionDeniedException(permission);
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class InMemoryAgentStateRepository : IAgentStateRepository
    {
        public List<AgentKillSwitchStateDto> Saved { get; } = [];

        public Task<IReadOnlyList<AgentKillSwitchStateDto>> ListStatesAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentKillSwitchStateDto>>(Saved.ToArray());

        public Task UpsertStateAsync(string tenantId, string workspaceId, AgentKillSwitchStateDto state, CancellationToken cancellationToken = default)
        {
            Saved.RemoveAll(s => string.Equals(s.AgentKey, state.AgentKey, StringComparison.OrdinalIgnoreCase));
            Saved.Add(state);
            return Task.CompletedTask;
        }
    }

    private sealed class NullAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationalTelemetry : IOperationalTelemetry
    {
        public List<(string TenantId, string WorkspaceId, string AgentKey, string Action, string Result)> AgentRuns { get; } = [];

        public void TrackAuditEventCreated(AuditEvent auditEvent) { }

        public void TrackAgentRegistryRun(string tenantId, string workspaceId, string agentKey, string action, string result) =>
            AgentRuns.Add((tenantId, workspaceId, agentKey, action, result));
    }
}
