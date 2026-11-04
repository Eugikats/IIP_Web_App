using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Domain.Knowledge;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Billing;

/// <summary>Writes the bare record of a model call into the action log, where the workspace's usage is totalled from.</summary>
public sealed class AgentActionLogUsageRecorder(
    IAgentActionLogRepository repository,
    IAiCostModel costModel,
    ILogger<AgentActionLogUsageRecorder> logger,
    TimeProvider? timeProvider = null) : IWorkspaceAiUsageRecorder
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public const string MeteringActor = "system:metering";

    public async Task RecordAsync(WorkspaceAiUsageEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.SaveAsync(
                new AgentActionLog
                {
                    AgentActionLogId = Guid.NewGuid(),
                    Scope = entry.Scope,
                    CaseId = entry.CaseId,
                    UserId = MeteringActor,
                    AgentName = entry.Operation,
                    AssistantMode = AssistantMode.OperationalSummary,
                    InputSummary = "Model call",
                    RetrievedSources = [],
                    ModelOutput = string.Empty,
                    ConfidenceScore = AiConfidenceScore.Create(0m),
                    RiskFlag = "None",
                    HumanOverride = false,
                    CreatedAt = clock.GetUtcNow(),
                    CorrelationId = entry.CorrelationId ?? string.Empty,
                    ModelProvider = entry.Provider,
                    ModelDeployment = entry.Deployment,
                    ModelVersion = entry.ModelVersion,
                    PromptVersion = "metering",
                    Visibility = "Internal",
                    PromptTokens = entry.PromptTokens ?? (entry.CompletionTokens is null ? null : 0),
                    CompletionTokens = entry.CompletionTokens ?? (entry.PromptTokens is null ? null : 0),
                    EstimatedCostUsd = costModel.Estimate(entry.Provider, entry.Deployment, entry.ModelVersion, entry.PromptTokens, entry.CompletionTokens),
                    RecordKind = ActionLogKinds.ModelCall,
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Counting must never stop the work being counted. The gap is logged so it can be noticed.
            logger.LogWarning("A model call could not be metered. ErrorType={ErrorType}", exception.GetType().Name);
        }
    }
}
