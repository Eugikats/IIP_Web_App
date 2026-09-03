using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Agents;

/// <summary>
/// Thin recording seam used by every AI call site alongside IAgentGate: one records whether the
/// agent may run, this records that it did (or was refused). Kept separate from AgentActionLog —
/// that type predates this work and captures a different shape (one row per model call, with the
/// model's own output); AgentRun is the higher-level "did this agent execute, with what tools,
/// to what outcome" record the architecture doc calls for (docs §11).
/// </summary>
public interface IAgentRunRecorder
{
    Task RecordAsync(
        TenantScope scope,
        string agentKey,
        string triggerType,
        IReadOnlyCollection<string> inputRefs,
        AgentRunStatus status,
        DateTimeOffset startedAt,
        string correlationId,
        string? userId,
        Guid? caseId,
        string? errorCategory = null,
        IReadOnlyCollection<AgentRunToolTraceEntry>? toolTrace = null,
        CancellationToken cancellationToken = default);
}

public sealed class AgentRunRecorder(IAgentRunRepository repository) : IAgentRunRecorder
{
    public Task RecordAsync(
        TenantScope scope,
        string agentKey,
        string triggerType,
        IReadOnlyCollection<string> inputRefs,
        AgentRunStatus status,
        DateTimeOffset startedAt,
        string correlationId,
        string? userId,
        Guid? caseId,
        string? errorCategory = null,
        IReadOnlyCollection<AgentRunToolTraceEntry>? toolTrace = null,
        CancellationToken cancellationToken = default) =>
        repository.SaveAsync(
            new AgentRun(
                Guid.NewGuid(),
                scope,
                agentKey,
                triggerType,
                inputRefs,
                status,
                startedAt,
                DateTimeOffset.UtcNow,
                errorCategory,
                correlationId,
                userId,
                caseId,
                toolTrace ?? Array.Empty<AgentRunToolTraceEntry>()),
            cancellationToken);
}
