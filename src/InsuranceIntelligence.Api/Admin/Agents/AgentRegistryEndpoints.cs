using InsuranceIntelligence.Application.Agents;

namespace InsuranceIntelligence.Api.Admin.Agents;

public static class AgentRegistryEndpoints
{
    public static async Task<IResult> ListAgents(
        IAgentRegistryService service,
        CancellationToken cancellationToken)
    {
        var agents = await service.ListAsync(cancellationToken);
        return Results.Ok(agents);
    }

    public static async Task<IResult> SetKillSwitch(
        string agentKey,
        KillSwitchRequest request,
        IAgentRegistryService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.SetKillSwitchAsync(agentKey, request.Enabled, request.Reason ?? string.Empty, cancellationToken);
            return Results.Ok(updated);
        }
        catch (AgentRegistryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }
}

public sealed record KillSwitchRequest(bool Enabled, string? Reason);
