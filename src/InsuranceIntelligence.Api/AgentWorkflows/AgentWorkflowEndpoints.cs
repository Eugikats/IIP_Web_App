using InsuranceIntelligence.Application.AgentWorkflows;

namespace InsuranceIntelligence.Api.AgentWorkflows;

public static class AgentWorkflowEndpoints
{
    public static async Task<IResult> ListDefinitions(IAgentWorkflowService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListDefinitionsAsync(cancellationToken));

    public static async Task<IResult> GetDefinition(string workflowKey, IAgentWorkflowService service, CancellationToken cancellationToken)
    {
        var definition = await service.GetDefinitionAsync(workflowKey, cancellationToken);
        return definition is null ? Results.NotFound() : Results.Ok(definition);
    }

    public static async Task<IResult> SaveDraft(
        SaveAgentWorkflowDraftRequest request,
        IAgentWorkflowService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var definition = await service.SaveDraftAsync(request, cancellationToken);
            return Results.Created($"/api/v1/agent-workflows/{definition.Key}", definition);
        });

    public static async Task<IResult> Publish(
        string workflowKey,
        IAgentWorkflowService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.PublishAsync(workflowKey, cancellationToken)));

    public static async Task<IResult> ListRuns(IAgentWorkflowService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListRunsAsync(cancellationToken));

    public static async Task<IResult> GetRun(Guid runId, IAgentWorkflowService service, CancellationToken cancellationToken)
    {
        var run = await service.GetRunAsync(runId, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(run);
    }

    public static async Task<IResult> StartRun(
        string workflowKey,
        StartAgentWorkflowRequest request,
        IAgentWorkflowService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var run = await service.StartAsync(workflowKey, request, cancellationToken);
            return Results.Accepted($"/api/v1/agent-workflow-runs/{run.RunId}", run);
        });

    public static async Task<IResult> CompleteNode(
        Guid runId,
        string nodeKey,
        CompleteAgentWorkflowNodeRequest request,
        IAgentWorkflowService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.CompleteNodeAsync(runId, nodeKey, request, cancellationToken)));

    public static async Task<IResult> ListWorkItems(IAgentWorkflowService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListWorkItemsAsync(cancellationToken));

    public static async Task<IResult> GetWorkItem(Guid workItemId, IAgentWorkflowService service, CancellationToken cancellationToken)
    {
        var workItem = await service.GetWorkItemAsync(workItemId, cancellationToken);
        return workItem is null ? Results.NotFound() : Results.Ok(workItem);
    }

    public static async Task<IResult> DecideWorkItem(
        Guid workItemId,
        DecideAgentWorkItemRequest request,
        IAgentWorkflowService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.DecideWorkItemAsync(workItemId, request, cancellationToken)));

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (AgentWorkflowValidationException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.Field] = [exception.Message] });
        }
    }
}

