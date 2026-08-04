using InsuranceIntelligence.Domain.Queue;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

public class AgentWorkflowExecutionFunction(
    IAgentWorkflowExecutionWorker worker,
    ILogger<AgentWorkflowExecutionFunction> logger)
{
    public const string AgentWorkflowQueueSetting = "agent-workflow";
    public const string ServiceBusConnectionSetting = "ServiceBusConnection";

    [Function("AgentWorkflowNodeExecute")]
    public async Task Run(
        [ServiceBusTrigger(AgentWorkflowQueueSetting, Connection = ServiceBusConnectionSetting)] string queueMessage,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing agent workflow execution message.");
        await worker.ExecuteAsync(queueMessage, cancellationToken);
    }
}
