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
        var service = new AgentRegistryService(Context(), new DenyPermissionService(), new InMemoryAgentStateRepository(), new NullAuditWriter());
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.ListAsync());
    }

    [Fact]
    public async Task SetKillSwitch_PersistsStateAndWritesAudit()
    {
        var repo = new InMemoryAgentStateRepository();
        var audit = new NullAuditWriter();
        var service = new AgentRegistryService(Context(), new AllowPermissionService(), repo, audit);
        var key = AgentCatalog.Agents[0].Key;

        var result = await service.SetKillSwitchAsync(key, enabled: false, "kill switch drill");

        Assert.False(result.Enabled);
        Assert.Single(repo.Saved, s => s.AgentKey == key && !s.Enabled);
        Assert.Single(audit.Events, e => e.ActionName == "agent.killswitch.disabled");

        var listed = await service.ListAsync();
        Assert.False(Assert.Single(listed, e => e.Key == key).Enabled);
    }

    [Fact]
    public async Task SetKillSwitch_UnknownAgent_Throws()
    {
        var service = new AgentRegistryService(Context(), new AllowPermissionService(), new InMemoryAgentStateRepository(), new NullAuditWriter());
        await Assert.ThrowsAsync<AgentRegistryValidationException>(() => service.SetKillSwitchAsync("no-such-agent", true, "x"));
    }

    [Fact]
    public async Task SetKillSwitch_MissingReason_Throws()
    {
        var service = new AgentRegistryService(Context(), new AllowPermissionService(), new InMemoryAgentStateRepository(), new NullAuditWriter());
        await Assert.ThrowsAsync<AgentRegistryValidationException>(() => service.SetKillSwitchAsync(AgentCatalog.Agents[0].Key, true, "  "));
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
}
