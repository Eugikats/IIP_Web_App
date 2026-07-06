using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Tenant-scoped persistence for agent kill-switch state (JSON payload). One
/// record per (tenant, workspace, agentKey), keyed by a deterministic id so
/// toggling upserts in place.
/// </summary>
public sealed class EfAgentStateRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentStateRepository
{
    public async Task<IReadOnlyList<AgentKillSwitchStateDto>> ListStatesAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);
        var payloads = await Scoped(DbContext.AgentStates.AsNoTracking(), scope)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        return payloads.Select(Deserialize<AgentKillSwitchStateDto>).ToArray();
    }

    public Task UpsertStateAsync(
        string tenantId,
        string workspaceId,
        AgentKillSwitchStateDto state,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);
        var id = DeterministicId(tenantId, workspaceId, state.AgentKey);
        return UpsertAsync(DbContext.AgentStates, id, scope, Serialize(state), cancellationToken: cancellationToken);
    }

    private static Guid DeterministicId(string tenantId, string workspaceId, string agentKey)
    {
        var composite = $"{tenantId}|{workspaceId}|{agentKey.ToLowerInvariant()}";
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(composite));
        return new Guid(bytes);
    }
}
