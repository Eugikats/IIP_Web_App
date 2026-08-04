using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfAgentWorkflowDefinitionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentWorkflowDefinitionRepository
{
    public async Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .OrderBy(record => record.WorkflowKey)
            .ThenBy(record => record.Version)
            .Select(record => Deserialize<AgentWorkflowDefinition>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentWorkflowDefinition>> ListByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .Where(item => item.WorkflowKey == normalized)
            .OrderBy(item => item.Version)
            .Select(record => Deserialize<AgentWorkflowDefinition>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AgentWorkflowDefinition?> GetAsync(Guid definitionId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        return record is null ? null : Deserialize<AgentWorkflowDefinition>(record.PayloadJson);
    }

    public async Task<AgentWorkflowDefinition?> GetByKeyAndVersionAsync(string key, int version, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var record = await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.WorkflowKey == normalized && item.Version == version, cancellationToken);
        return record is null ? null : Deserialize<AgentWorkflowDefinition>(record.PayloadJson);
    }

    public async Task<AgentWorkflowDefinition?> GetDraftByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var draftStatus = AgentWorkflowDefinitionStatus.Draft.ToString();
        var record = await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .Where(item => item.WorkflowKey == normalized && item.Status == draftStatus)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : Deserialize<AgentWorkflowDefinition>(record.PayloadJson);
    }

    public async Task<AgentWorkflowDefinition?> GetLatestPublishedByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var publishedStatus = AgentWorkflowDefinitionStatus.Published.ToString();
        var record = await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .Where(item => item.WorkflowKey == normalized && item.Status == publishedStatus)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : Deserialize<AgentWorkflowDefinition>(record.PayloadJson);
    }

    public async Task<int> GetMaxVersionAsync(string key, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        var max = await Scoped(DbContext.AgentWorkflowDefinitions.AsNoTracking(), scope)
            .Where(item => item.WorkflowKey == normalized)
            .Select(item => (int?)item.Version)
            .MaxAsync(cancellationToken);
        return max ?? 0;
    }

    public async Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var scope = definition.Scope ?? throw new InvalidOperationException("Custom workflow definitions must have a tenant/workspace scope.");
        try
        {
            await UpsertAsync(
                DbContext.AgentWorkflowDefinitions,
                definition.DefinitionId,
                scope,
                Serialize(definition),
                record =>
                {
                    record.WorkflowKey = definition.Key;
                    record.Status = definition.Status.ToString();
                    record.Version = definition.Version;
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new AgentWorkflowValidationException(
                "key",
                "A workflow definition with this key and version already exists.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is SqlException sql && (sql.Number is 2601 or 2627))
            {
                return true;
            }
        }

        return exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
            || exception.Message.Contains("unique", StringComparison.OrdinalIgnoreCase);
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
                record.DefinitionId = run.DefinitionId;
                record.WorkflowVersion = run.WorkflowVersion;
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
