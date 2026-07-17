using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.AgentWorkflows;

public interface IAgentWorkflowDefinitionRepository
{
    Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
    Task<AgentWorkflowDefinition?> GetAsync(Guid definitionId, TenantScope scope, CancellationToken cancellationToken = default);
    Task<AgentWorkflowDefinition?> GetByKeyAsync(string key, TenantScope scope, CancellationToken cancellationToken = default);
    Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken cancellationToken = default);
}

public interface IAgentWorkflowRunRepository
{
    Task<IReadOnlyList<AgentWorkflowRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
    Task<AgentWorkflowRun?> GetAsync(Guid runId, TenantScope scope, CancellationToken cancellationToken = default);
    Task SaveAsync(AgentWorkflowRun run, CancellationToken cancellationToken = default);
}

public interface IAgentWorkItemRepository
{
    Task<IReadOnlyList<AgentWorkItem>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
    Task<AgentWorkItem?> GetAsync(Guid workItemId, TenantScope scope, CancellationToken cancellationToken = default);
    Task<AgentWorkItem?> GetForNodeAsync(Guid runId, string nodeKey, TenantScope scope, CancellationToken cancellationToken = default);
    Task SaveAsync(AgentWorkItem workItem, CancellationToken cancellationToken = default);
}

public sealed record SaveAgentWorkflowDraftRequest(
    Guid? DefinitionId,
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> BusinessReferenceTypes,
    IReadOnlyList<AgentWorkflowNode> Nodes,
    IReadOnlyList<AgentWorkflowEdge> Edges);

public sealed record StartAgentWorkflowRequest(
    string BusinessReferenceType,
    string BusinessReferenceId,
    string? CorrelationId);

public sealed record CompleteAgentWorkflowNodeRequest(
    string? Outcome,
    string OutputSummary,
    IReadOnlyList<AgentEvidenceReference> Evidence,
    string? Provider,
    string? ModelOrTool,
    string? PromptVersion,
    string? ErrorCode);

public sealed record DecideAgentWorkItemRequest(
    AgentWorkItemDecision Decision,
    string Reason);

public interface IAgentWorkflowService
{
    Task<IReadOnlyList<AgentWorkflowDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<AgentWorkflowDefinition?> GetDefinitionAsync(string key, CancellationToken cancellationToken = default);
    Task<AgentWorkflowDefinition> SaveDraftAsync(SaveAgentWorkflowDraftRequest request, CancellationToken cancellationToken = default);
    Task<AgentWorkflowDefinition> PublishAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentWorkflowRun>> ListRunsAsync(CancellationToken cancellationToken = default);
    Task<AgentWorkflowRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<AgentWorkflowRun> StartAsync(string workflowKey, StartAgentWorkflowRequest request, CancellationToken cancellationToken = default);
    Task<AgentWorkflowRun> CompleteNodeAsync(Guid runId, string nodeKey, CompleteAgentWorkflowNodeRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentWorkItem>> ListWorkItemsAsync(CancellationToken cancellationToken = default);
    Task<AgentWorkItem?> GetWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<AgentWorkItem> DecideWorkItemAsync(Guid workItemId, DecideAgentWorkItemRequest request, CancellationToken cancellationToken = default);
}

public sealed class AgentWorkflowValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

