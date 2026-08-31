using System.Text.Json;
using InsuranceIntelligence.Application.AgentWorkflows;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Queue;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

public interface IAgentWorkflowExecutionWorker
{
    Task ExecuteAsync(string queueMessage, CancellationToken cancellationToken = default);
}

public sealed class AgentWorkflowExecutionWorker(
    ITenantContextSetter tenantContextSetter,
    IPermissionReadRepository permissionReadRepository,
    IAgentWorkflowService workflowService,
    IAgentWorkflowNodeExecutor nodeExecutor,
    ILogger<AgentWorkflowExecutionWorker> logger,
    Infrastructure.Configuration.InfrastructureOptions options) : IAgentWorkflowExecutionWorker
{
    public const string AgentWorkflowMessageType = "agent-workflow.node.execute.v1";
    public const string DefaultWorkerUserId = "system:agent-workflow-worker";

    public async Task ExecuteAsync(string queueMessage, CancellationToken cancellationToken = default)
    {
        var envelope = DeserializeEnvelope(queueMessage);
        if (!envelope.MessageType.Equals(AgentWorkflowMessageType, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported message type '{envelope.MessageType}'.");
        }

        var payload = ParsePayload(envelope.Payload);
        if (payload.RunId == Guid.Empty || payload.DefinitionId == Guid.Empty || payload.Attempt <= 0)
        {
            throw new InvalidDataException("Agent workflow payload is missing required identifiers.");
        }

        var workerUserId = string.IsNullOrWhiteSpace(options.ServiceBus.AgentWorkflowWorkerUserId)
            ? DefaultWorkerUserId
            : options.ServiceBus.AgentWorkflowWorkerUserId.Trim();

        var membership = await permissionReadRepository.GetEffectiveMembershipAsync(
            workerUserId,
            envelope.TenantId,
            envelope.WorkspaceId,
            cancellationToken);
        if (membership is null)
        {
            throw new InvalidOperationException(
                $"Agent workflow worker principal '{workerUserId}' has no active membership for tenant/workspace.");
        }

        // Envelope tenant/workspace are dispatch metadata, not authority. Membership is authority;
        // reject confused-deputy cases where the store returns a different scope than the message.
        if (!membership.TenantId.Equals(envelope.TenantId, StringComparison.OrdinalIgnoreCase)
            || !membership.WorkspaceId.Equals(envelope.WorkspaceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Agent workflow worker membership scope does not match the queue envelope tenant/workspace.");
        }

        tenantContextSetter.SetCurrent(new TenantContext(
            membership.TenantId,
            membership.WorkspaceId,
            membership.UserId,
            membership.Roles));

        var run = await workflowService.GetRunAsync(payload.RunId, cancellationToken)
            ?? throw new InvalidDataException($"Workflow run '{payload.RunId}' was not found for this tenant/workspace.");
        if (run.DefinitionId != payload.DefinitionId
            || run.WorkflowVersion != payload.WorkflowVersion
            || !run.WorkflowKey.Equals(payload.WorkflowKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Workflow dispatch payload definition/version does not match the persisted run.");
        }

        var nodeRun = run.NodeRuns.FirstOrDefault(item =>
            item.NodeKey.Equals(payload.NodeKey, StringComparison.OrdinalIgnoreCase));
        if (nodeRun is null)
        {
            throw new InvalidDataException($"Node '{payload.NodeKey}' does not exist on run '{payload.RunId}'.");
        }

        // Duplicate and out-of-order deliveries are acknowledged without replaying stale completion.
        if (nodeRun.AttemptCount > payload.Attempt
            || nodeRun.Status is AgentWorkflowNodeRunStatus.Completed
                or AgentWorkflowNodeRunStatus.Failed
                or AgentWorkflowNodeRunStatus.Skipped
                or AgentWorkflowNodeRunStatus.WaitingForHuman)
        {
            logger.LogInformation(
                "Skipping duplicate or stale agent-workflow message. Run={RunId} Node={NodeKey} Attempt={Attempt} Status={Status} ExistingAttempt={ExistingAttempt}",
                payload.RunId,
                payload.NodeKey,
                payload.Attempt,
                nodeRun.Status,
                nodeRun.AttemptCount);
            return;
        }

        if (payload.NodeType is nameof(AgentWorkflowNodeType.HumanReview)
            or nameof(AgentWorkflowNodeType.Complete)
            or nameof(AgentWorkflowNodeType.Trigger))
        {
            logger.LogInformation(
                "Stopping at governed non-executable node. Run={RunId} Node={NodeKey} Type={NodeType}",
                payload.RunId,
                payload.NodeKey,
                payload.NodeType);
            return;
        }

        if (payload.NodeType == nameof(AgentWorkflowNodeType.Agent))
        {
            if (string.IsNullOrWhiteSpace(payload.AgentKey))
            {
                throw new InvalidDataException("Agent node dispatch is missing AgentKey.");
            }
        }
        else if (payload.NodeType is nameof(AgentWorkflowNodeType.Tool) or nameof(AgentWorkflowNodeType.Decision))
        {
            if (string.IsNullOrWhiteSpace(payload.ToolKey))
            {
                throw new InvalidDataException("Tool/decision node dispatch is missing ToolKey.");
            }
        }
        else
        {
            throw new InvalidDataException($"Unsupported executable node type '{payload.NodeType}'.");
        }

        var completion = await nodeExecutor.ExecuteAsync(
            new AgentWorkflowExecutionContext(
                run.Scope,
                payload,
                run.BusinessReferenceType,
                run.BusinessReferenceId),
            cancellationToken);
        await workflowService.CompleteNodeAsync(payload.RunId, payload.NodeKey, completion, cancellationToken);
    }

    private static QueueMessageEnvelope DeserializeEnvelope(string queueMessage)
    {
        if (string.IsNullOrWhiteSpace(queueMessage))
        {
            throw new InvalidDataException("Queue message body is required.");
        }

        var envelope = JsonSerializer.Deserialize<QueueMessageEnvelope>(queueMessage);
        if (envelope is null
            || string.IsNullOrWhiteSpace(envelope.MessageType)
            || string.IsNullOrWhiteSpace(envelope.TenantId)
            || string.IsNullOrWhiteSpace(envelope.WorkspaceId)
            || string.IsNullOrWhiteSpace(envelope.CorrelationId)
            || envelope.Payload is null)
        {
            throw new InvalidDataException("Queue envelope is invalid.");
        }

        return envelope;
    }

    private static AgentWorkflowNodeDispatch ParsePayload(object payload)
    {
        if (payload is not JsonElement payloadElement || payloadElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Queue payload is invalid.");
        }

        var parsed = payloadElement.Deserialize<AgentWorkflowNodeDispatch>();
        if (parsed is null)
        {
            throw new InvalidDataException("Queue payload could not be deserialized.");
        }

        return parsed;
    }
}

public sealed record AgentWorkflowExecutionContext(
    TenantScope Scope,
    AgentWorkflowNodeDispatch Dispatch,
    string BusinessReferenceType,
    string BusinessReferenceId);

public interface IAgentWorkflowNodeExecutor
{
    Task<CompleteAgentWorkflowNodeRequest> ExecuteAsync(
        AgentWorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class AgentWorkflowNodeExecutor(
    ICaseDocumentRepository caseDocumentRepository) : IAgentWorkflowNodeExecutor
{
    public Task<CompleteAgentWorkflowNodeRequest> ExecuteAsync(
        AgentWorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Dispatch.NodeType == nameof(AgentWorkflowNodeType.Agent))
        {
            return ExecuteAgentNodeAsync(context, cancellationToken);
        }

        if (context.Dispatch.NodeType is nameof(AgentWorkflowNodeType.Tool) or nameof(AgentWorkflowNodeType.Decision))
        {
            return ExecuteToolNodeAsync(context, cancellationToken);
        }

        throw new InvalidDataException($"Unsupported executable node type '{context.Dispatch.NodeType}'.");
    }

    private async Task<CompleteAgentWorkflowNodeRequest> ExecuteAgentNodeAsync(
        AgentWorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Dispatch.AgentKey!.Equals("document-checker", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Agent '{context.Dispatch.AgentKey}' is outside the Release 1 allow-list.");
        }

        if (!context.BusinessReferenceType.Equals("case", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(context.BusinessReferenceId, out var caseId))
        {
            throw new InvalidDataException("Document checker requires businessReferenceType=case with a valid case GUID.");
        }

        var documents = await caseDocumentRepository.ListForCaseAsync(caseId, context.Scope, cancellationToken);
        var evidence = documents
            .Take(50)
            .Select(document => new AgentEvidenceReference(
                "document",
                document.DocumentId.ToString(),
                document.OriginalFileName,
                null,
                null,
                document.Classification.ConfidenceScore,
                null))
            .ToArray();

        var summary = documents.Count == 0
            ? "No documents were available for this case."
            : $"Checked {documents.Count} document(s) using persisted extraction metadata only.";

        return new CompleteAgentWorkflowNodeRequest(
            Outcome: "ready",
            OutputSummary: summary,
            Evidence: evidence,
            Provider: "IIPFunctionsWorker",
            ModelOrTool: "document-checker",
            PromptVersion: "rf06-v1",
            ErrorCode: null);
    }

    private static Task<CompleteAgentWorkflowNodeRequest> ExecuteToolNodeAsync(
        AgentWorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(context.Dispatch.ToolKey)
            || !context.Dispatch.ToolKey.Equals("workflow.evidence-quality", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Tool '{context.Dispatch.ToolKey ?? "(missing)"}' is outside the Release 1 allow-list.");
        }

        return Task.FromResult(new CompleteAgentWorkflowNodeRequest(
            Outcome: "review",
            OutputSummary: "Evidence quality gate completed on bounded metadata; human extraction review remains required.",
            Evidence: [],
            Provider: "IIPFunctionsWorker",
            ModelOrTool: "workflow.evidence-quality",
            PromptVersion: "rf06-v1",
            ErrorCode: null));
    }
}
