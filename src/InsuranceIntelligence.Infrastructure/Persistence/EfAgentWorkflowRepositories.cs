using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfAgentWorkflowDefinitionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentWorkflowDefinitionRepository
{
    public async Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .OrderBy(record => record.WorkflowKey)
            .Select(record => Deserialize<AgentWorkflowDefinition>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<AgentWorkflowDefinition?> GetAsync(Guid definitionId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        return record is null ? null : Deserialize<AgentWorkflowDefinition>(record.PayloadJson);
    }

    public async Task<AgentWorkflowDefinition?> GetByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var record = await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.WorkflowKey == normalized, cancellationToken);
        return record is null ? null : Deserialize<AgentWorkflowDefinition>(record.PayloadJson);
    }

    public Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var scope = definition.Scope ?? throw new InvalidOperationException("Custom workflow definitions must have a tenant/workspace scope.");
        return UpsertAsync(
            DbContext.AgentWorkflowDefinitions,
            definition.DefinitionId,
            scope,
            Serialize(definition),
            record =>
            {
                record.WorkflowKey = definition.Key;
                record.Status = definition.Status.ToString();
            },
            cancellationToken);
    }
}

public sealed class EfAgentWorkflowRunRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentWorkflowRunRepository
{
    public async Task<IReadOnlyList<AgentWorkflowRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentWorkflowRuns.AsNoTracking(), scope)
            .OrderByDescending(record => record.UpdatedAt)
            .Select(record => Deserialize<AgentWorkflowRun>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<AgentWorkflowRun?> GetAsync(Guid runId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AgentWorkflowRuns.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);
        return record is null ? null : Deserialize<AgentWorkflowRun>(record.PayloadJson);
    }

    public Task SaveAsync(AgentWorkflowRun run, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AgentWorkflowRuns,
            run.RunId,
            run.Scope,
            Serialize(run),
            record =>
            {
                record.WorkflowKey = run.WorkflowKey;
                record.Status = run.Status.ToString();
                record.BusinessReferenceId = run.BusinessReferenceId;
            },
            cancellationToken);
}

public sealed class EfAgentWorkItemRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentWorkItemRepository
{
    public async Task<IReadOnlyList<AgentWorkItem>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentWorkItems.AsNoTracking(), scope)
            .OrderByDescending(record => record.UpdatedAt)
            .Select(record => Deserialize<AgentWorkItem>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<AgentWorkItem?> GetAsync(Guid workItemId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AgentWorkItems.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == workItemId, cancellationToken);
        return record is null ? null : Deserialize<AgentWorkItem>(record.PayloadJson);
    }

    public async Task<AgentWorkItem?> GetForNodeAsync(Guid runId, string nodeKey, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AgentWorkItems.AsNoTracking(), scope)
            .Where(item => item.RunId == runId && item.NodeKey == nodeKey)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : Deserialize<AgentWorkItem>(record.PayloadJson);
    }

    public Task SaveAsync(AgentWorkItem workItem, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AgentWorkItems,
            workItem.WorkItemId,
            workItem.Scope,
            Serialize(workItem),
            record =>
            {
                record.RunId = workItem.RunId;
                record.WorkflowKey = workItem.WorkflowKey;
                record.NodeKey = workItem.NodeKey;
                record.Status = workItem.Status.ToString();
            },
            cancellationToken);
}

