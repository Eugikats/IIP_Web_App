using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.AgentWorkflows;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Queue;

namespace InsuranceIntelligence.Application.AgentWorkflows;

public sealed class AgentWorkflowService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAgentWorkflowDefinitionRepository definitionRepository,
    IAgentWorkflowRunRepository runRepository,
    IAgentWorkItemRepository workItemRepository,
    IMessageQueueDispatcher queueDispatcher,
    IAuditWriter auditWriter) : IAgentWorkflowService
{
    public const string QueueName = "agent-workflow";

    public async Task<IReadOnlyList<AgentWorkflowDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewAgentWorkflows, cancellationToken);
        var custom = await definitionRepository.ListAsync(tenantContextAccessor.Current.Scope, cancellationToken);
        return AgentWorkflowCatalog.Releases
            .Concat(custom)
            .OrderBy(item => item.Release == 0 ? int.MaxValue : item.Release)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<AgentWorkflowDefinition?> GetDefinitionAsync(string key, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewAgentWorkflows, cancellationToken);
        return await ResolveDefinitionAsync(key, cancellationToken);
    }

    public async Task<AgentWorkflowDefinition> SaveDraftAsync(
        SaveAgentWorkflowDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageAgentWorkflows, cancellationToken);
        ValidateDefinitionInput(request.Key, request.Name, request.BusinessReferenceTypes, request.Nodes, request.Edges);

        if (AgentWorkflowCatalog.Find(request.Key) is not null)
        {
            throw new AgentWorkflowValidationException(nameof(request.Key), "System release workflows are immutable. Copy one to a new key before editing.");
        }

        var context = tenantContextAccessor.Current;
        var existing = request.DefinitionId.HasValue
            ? await definitionRepository.GetAsync(request.DefinitionId.Value, context.Scope, cancellationToken)
            : await definitionRepository.GetByKeyAsync(request.Key, context.Scope, cancellationToken);

        if (existing is not null && existing.Status == AgentWorkflowDefinitionStatus.Published)
        {
            throw new AgentWorkflowValidationException(nameof(request.DefinitionId), "Published workflow versions are immutable. Create a new workflow key for a revised version.");
        }

        var now = DateTimeOffset.UtcNow;
        var definition = new AgentWorkflowDefinition(
            existing?.DefinitionId ?? request.DefinitionId ?? Guid.NewGuid(),
            context.Scope,
            NormalizeKey(request.Key),
            request.Name.Trim(),
            request.Description.Trim(),
            Release: 0,
            Version: existing?.Version ?? 1,
            AgentWorkflowDefinitionStatus.Draft,
            IsSystemDefinition: false,
            request.BusinessReferenceTypes.Select(item => item.Trim().ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            request.Nodes,
            request.Edges,
            existing?.CreatedAt ?? now,
            existing?.CreatedByUserId ?? context.UserId,
            PublishedAt: null,
            PublishedByUserId: null);

        await definitionRepository.SaveAsync(definition, cancellationToken);
        await WriteAuditAsync("agent_workflow.draft.saved", definition.Key, definition.Status.ToString(), definition.DefinitionId.ToString(), null, cancellationToken);
        return definition;
    }

    public async Task<AgentWorkflowDefinition> PublishAsync(string key, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageAgentWorkflows, cancellationToken);
        if (AgentWorkflowCatalog.Find(key) is not null)
        {
            throw new AgentWorkflowValidationException(nameof(key), "System release workflows are already published and immutable.");
        }

        var context = tenantContextAccessor.Current;
        var definition = await definitionRepository.GetByKeyAsync(key, context.Scope, cancellationToken)
            ?? throw new AgentWorkflowValidationException(nameof(key), "Workflow definition was not found.");

        ValidateDefinitionInput(
            definition.Key,
            definition.Name,
            definition.BusinessReferenceTypes,
            definition.Nodes,
            definition.Edges);

        var published = definition with
        {
            Status = AgentWorkflowDefinitionStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedByUserId = context.UserId
        };
        await definitionRepository.SaveAsync(published, cancellationToken);
        await WriteAuditAsync("agent_workflow.published", published.Key, published.Status.ToString(), published.DefinitionId.ToString(), null, cancellationToken);
        return published;
    }

    public async Task<IReadOnlyList<AgentWorkflowRun>> ListRunsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewAgentWorkflows, cancellationToken);
        var runs = await runRepository.ListAsync(tenantContextAccessor.Current.Scope, cancellationToken);
        return runs.OrderByDescending(item => item.UpdatedAt).ToArray();
    }

    public async Task<AgentWorkflowRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewAgentWorkflows, cancellationToken);
        return await runRepository.GetAsync(runId, tenantContextAccessor.Current.Scope, cancellationToken);
    }

    public async Task<AgentWorkflowRun> StartAsync(
        string workflowKey,
        StartAgentWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.RunAgentWorkflows, cancellationToken);
        var definition = await ResolveDefinitionAsync(workflowKey, cancellationToken)
            ?? throw new AgentWorkflowValidationException(nameof(workflowKey), "Workflow definition was not found.");

        if (definition.Status != AgentWorkflowDefinitionStatus.Published)
        {
            throw new AgentWorkflowValidationException(nameof(workflowKey), "Only published workflow definitions can run.");
        }

        AgentWorkflowReleaseOneBound.EnsureExecutable(definition);

        var referenceType = request.BusinessReferenceType?.Trim().ToLowerInvariant() ?? string.Empty;
        var referenceId = request.BusinessReferenceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(referenceType)
            || !definition.BusinessReferenceTypes.Contains(referenceType, StringComparer.OrdinalIgnoreCase))
        {
            throw new AgentWorkflowValidationException(nameof(request.BusinessReferenceType), "A supported business reference type is required.");
        }
        if (string.IsNullOrWhiteSpace(referenceId) || referenceId.Length > 240)
        {
            throw new AgentWorkflowValidationException(nameof(request.BusinessReferenceId), "A real business record reference is required (maximum 240 characters).");
        }

        var context = tenantContextAccessor.Current;
        var now = DateTimeOffset.UtcNow;
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId.Trim();
        var nodeRuns = definition.Nodes
            .Select(node => new AgentWorkflowNodeRun(
                node.Key,
                node.Type == AgentWorkflowNodeType.Trigger ? AgentWorkflowNodeRunStatus.Completed : AgentWorkflowNodeRunStatus.Pending,
                node.Type == AgentWorkflowNodeType.Trigger ? now : null,
                node.Type == AgentWorkflowNodeType.Trigger ? now : null,
                node.Type == AgentWorkflowNodeType.Trigger ? "received" : null,
                node.Type == AgentWorkflowNodeType.Trigger ? "Validated business reference received." : null,
                Array.Empty<AgentEvidenceReference>(),
                Provider: null,
                ModelOrTool: null,
                PromptVersion: null,
                ErrorCode: null,
                AttemptCount: node.Type == AgentWorkflowNodeType.Trigger ? 1 : 0))
            .ToArray();

        var run = new AgentWorkflowRun(
            Guid.NewGuid(),
            context.Scope,
            definition.DefinitionId,
            definition.Key,
            definition.Version,
            referenceType,
            referenceId,
            AgentWorkflowRunStatus.Queued,
            nodeRuns,
            now,
            now,
            CompletedAt: null,
            context.UserId,
            correlationId,
            FailureCode: null);

        await runRepository.SaveAsync(run, cancellationToken);
        await WriteAuditAsync("agent_workflow.run.started", definition.Key, run.Status.ToString(), run.RunId.ToString(), referenceId, cancellationToken, correlationId);
        return await ActivateReadyNodesAsync(definition, run, cancellationToken);
    }

    public async Task<AgentWorkflowRun> CompleteNodeAsync(
        Guid runId,
        string nodeKey,
        CompleteAgentWorkflowNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageAgentWorkflows, cancellationToken);
        var context = tenantContextAccessor.Current;
        var run = await runRepository.GetAsync(runId, context.Scope, cancellationToken)
            ?? throw new AgentWorkflowValidationException(nameof(runId), "Workflow run was not found.");
        var definition = await ResolveDefinitionAsync(run.WorkflowKey, cancellationToken)
            ?? throw new AgentWorkflowValidationException(nameof(run.WorkflowKey), "Workflow definition was not found.");
        var node = definition.Nodes.FirstOrDefault(item => string.Equals(item.Key, nodeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new AgentWorkflowValidationException(nameof(nodeKey), "Workflow node was not found.");
        if (node.Type is AgentWorkflowNodeType.Trigger or AgentWorkflowNodeType.HumanReview or AgentWorkflowNodeType.Complete)
        {
            throw new AgentWorkflowValidationException(nameof(nodeKey), "This node is controlled by the workflow engine and cannot be completed by a worker.");
        }

        var nodeRun = FindNodeRun(run, node.Key);
        if (nodeRun.Status is not (AgentWorkflowNodeRunStatus.Queued or AgentWorkflowNodeRunStatus.Running))
        {
            throw new AgentWorkflowValidationException(nameof(nodeKey), "Only a queued or running node can be completed.");
        }
        if (request.Evidence.Count > 50
            || string.IsNullOrWhiteSpace(request.OutputSummary)
            || request.OutputSummary.Length > 4000)
        {
            throw new AgentWorkflowValidationException(
                nameof(request),
                "A bounded node output summary is required (maximum 4,000 characters and 50 evidence references).");
        }

        var now = DateTimeOffset.UtcNow;
        var failed = !string.IsNullOrWhiteSpace(request.ErrorCode);
        var updatedNode = nodeRun with
        {
            Status = failed ? AgentWorkflowNodeRunStatus.Failed : AgentWorkflowNodeRunStatus.Completed,
            CompletedAt = now,
            Outcome = NormalizeOptional(request.Outcome),
            OutputSummary = request.OutputSummary.Trim(),
            Evidence = request.Evidence.ToArray(),
            Provider = NormalizeOptional(request.Provider),
            ModelOrTool = NormalizeOptional(request.ModelOrTool),
            PromptVersion = NormalizeOptional(request.PromptVersion),
            ErrorCode = NormalizeOptional(request.ErrorCode)
        };
        var updated = ReplaceNodeRun(run, updatedNode) with
        {
            Status = failed ? AgentWorkflowRunStatus.Failed : AgentWorkflowRunStatus.Running,
            UpdatedAt = now,
            CompletedAt = failed ? now : null,
            FailureCode = updatedNode.ErrorCode
        };
        await runRepository.SaveAsync(updated, cancellationToken);
        await WriteAuditAsync(
            failed ? "agent_workflow.node.failed" : "agent_workflow.node.completed",
            node.Key,
            updatedNode.Status.ToString(),
            run.RunId.ToString(),
            request.Evidence.FirstOrDefault()?.SourceId,
            cancellationToken,
            run.CorrelationId,
            request.Provider,
            request.Evidence.FirstOrDefault()?.Confidence);
        return failed ? updated : await ActivateReadyNodesAsync(definition, updated, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentWorkItem>> ListWorkItemsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ReviewAgentWorkItems, cancellationToken);
        var items = await workItemRepository.ListAsync(tenantContextAccessor.Current.Scope, cancellationToken);
        return items.OrderByDescending(item => item.UpdatedAt).ToArray();
    }

    public async Task<AgentWorkItem?> GetWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ReviewAgentWorkItems, cancellationToken);
        return await workItemRepository.GetAsync(workItemId, tenantContextAccessor.Current.Scope, cancellationToken);
    }

    public async Task<AgentWorkItem> DecideWorkItemAsync(
        Guid workItemId,
        DecideAgentWorkItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ReviewAgentWorkItems, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3)
        {
            throw new AgentWorkflowValidationException(nameof(request.Reason), "A review reason is required.");
        }

        var context = tenantContextAccessor.Current;
        ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
            requiresHumanDecision: true,
            ApprovalDecisionGuard.ResolveActorType(context.UserId),
            requestedActionType: "agent_workflow.work_item.decide");

        var item = await workItemRepository.GetAsync(workItemId, context.Scope, cancellationToken)
            ?? throw new AgentWorkflowValidationException(nameof(workItemId), "Work item was not found.");
        if (item.Status is not (AgentWorkItemStatus.RequiresReview or AgentWorkItemStatus.InProgress or AgentWorkItemStatus.Escalated))
        {
            throw new AgentWorkflowValidationException(nameof(workItemId), "This work item is no longer awaiting a decision.");
        }

        var run = await runRepository.GetAsync(item.RunId, context.Scope, cancellationToken)
            ?? throw new AgentWorkflowValidationException(nameof(item.RunId), "Workflow run was not found.");
        var definition = await ResolveDefinitionAsync(run.WorkflowKey, cancellationToken)
            ?? throw new AgentWorkflowValidationException(nameof(run.WorkflowKey), "Workflow definition was not found.");
        var reviewNode = definition.Nodes.First(node => node.Key.Equals(item.NodeKey, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow;
        var decidedItem = item with
        {
            Status = request.Decision switch
            {
                AgentWorkItemDecision.AcceptPreparedWork => AgentWorkItemStatus.Completed,
                AgentWorkItemDecision.ReturnForCorrection => AgentWorkItemStatus.ReturnedForCorrection,
                AgentWorkItemDecision.Escalate => AgentWorkItemStatus.Escalated,
                AgentWorkItemDecision.CancelWorkflow => AgentWorkItemStatus.Cancelled,
                _ => throw new AgentWorkflowValidationException(nameof(request.Decision), "Unsupported work item decision.")
            },
            UpdatedAt = now,
            DecidedAt = now,
            DecidedByUserId = context.UserId,
            Decision = request.Decision,
            DecisionReason = request.Reason.Trim()
        };
        await workItemRepository.SaveAsync(decidedItem, cancellationToken);

        AgentWorkflowRun updatedRun;
        if (request.Decision == AgentWorkItemDecision.AcceptPreparedWork)
        {
            var nodeRun = FindNodeRun(run, reviewNode.Key) with
            {
                Status = AgentWorkflowNodeRunStatus.Completed,
                CompletedAt = now,
                Outcome = "accepted",
                OutputSummary = "Prepared workflow work accepted by an accountable human reviewer."
            };
            updatedRun = ReplaceNodeRun(run, nodeRun) with { Status = AgentWorkflowRunStatus.Running, UpdatedAt = now };
            await runRepository.SaveAsync(updatedRun, cancellationToken);
            updatedRun = await ActivateReadyNodesAsync(definition, updatedRun, cancellationToken);
        }
        else if (request.Decision == AgentWorkItemDecision.ReturnForCorrection)
        {
            if (!reviewNode.Configuration.TryGetValue("correctionTarget", out var correctionTarget))
            {
                throw new AgentWorkflowValidationException(nameof(request.Decision), "This review node has no governed correction target.");
            }
            var resetReview = FindNodeRun(run, reviewNode.Key) with
            {
                Status = AgentWorkflowNodeRunStatus.Pending,
                StartedAt = null,
                CompletedAt = null,
                Outcome = null,
                OutputSummary = null
            };
            var resetTarget = FindNodeRun(ReplaceNodeRun(run, resetReview), correctionTarget) with
            {
                Status = AgentWorkflowNodeRunStatus.Pending,
                StartedAt = null,
                CompletedAt = null,
                Outcome = null,
                OutputSummary = null,
                ErrorCode = null
            };
            updatedRun = ReplaceNodeRun(ReplaceNodeRun(run, resetReview), resetTarget) with
            {
                Status = AgentWorkflowRunStatus.Running,
                UpdatedAt = now,
                CompletedAt = null,
                FailureCode = null
            };
            await runRepository.SaveAsync(updatedRun, cancellationToken);
            updatedRun = await ActivateReadyNodesAsync(definition, updatedRun, cancellationToken);
        }
        else if (request.Decision == AgentWorkItemDecision.CancelWorkflow)
        {
            updatedRun = run with { Status = AgentWorkflowRunStatus.Cancelled, UpdatedAt = now, CompletedAt = now, FailureCode = "cancelled-by-reviewer" };
            await runRepository.SaveAsync(updatedRun, cancellationToken);
        }
        else
        {
            updatedRun = run with { Status = AgentWorkflowRunStatus.WaitingForHuman, UpdatedAt = now };
            await runRepository.SaveAsync(updatedRun, cancellationToken);
        }

        await WriteAuditAsync(
            "agent_workflow.work_item.decided",
            item.NodeKey,
            decidedItem.Status.ToString(),
            item.WorkItemId.ToString(),
            item.BusinessReferenceId,
            cancellationToken,
            item.CorrelationId,
            auditReason: request.Reason.Trim());
        return decidedItem;
    }

    private async Task<AgentWorkflowRun> ActivateReadyNodesAsync(
        AgentWorkflowDefinition definition,
        AgentWorkflowRun initialRun,
        CancellationToken cancellationToken)
    {
        var run = initialRun;
        var madeProgress = true;
        while (madeProgress && run.Status is not (AgentWorkflowRunStatus.Failed or AgentWorkflowRunStatus.Cancelled or AgentWorkflowRunStatus.Completed))
        {
            madeProgress = false;
            foreach (var node in definition.Nodes.Where(node => FindNodeRun(run, node.Key).Status == AgentWorkflowNodeRunStatus.Pending))
            {
                if (!IsReady(definition, run, node))
                {
                    continue;
                }

                madeProgress = true;
                if (node.Type == AgentWorkflowNodeType.Complete)
                {
                    var now = DateTimeOffset.UtcNow;
                    var completedNode = FindNodeRun(run, node.Key) with
                    {
                        Status = AgentWorkflowNodeRunStatus.Completed,
                        StartedAt = now,
                        CompletedAt = now,
                        Outcome = "completed",
                        OutputSummary = "Workflow preparation completed. No consequential insurance decision or external send was executed."
                    };
                    run = ReplaceNodeRun(run, completedNode) with
                    {
                        Status = AgentWorkflowRunStatus.Completed,
                        UpdatedAt = now,
                        CompletedAt = now
                    };
                    await runRepository.SaveAsync(run, cancellationToken);
                    await WriteAuditAsync("agent_workflow.run.completed", run.WorkflowKey, run.Status.ToString(), run.RunId.ToString(), run.BusinessReferenceId, cancellationToken, run.CorrelationId);
                    break;
                }

                if (node.Type == AgentWorkflowNodeType.HumanReview)
                {
                    var now = DateTimeOffset.UtcNow;
                    var waitingNode = FindNodeRun(run, node.Key) with
                    {
                        Status = AgentWorkflowNodeRunStatus.WaitingForHuman,
                        StartedAt = now,
                        AttemptCount = FindNodeRun(run, node.Key).AttemptCount + 1
                    };
                    run = ReplaceNodeRun(run, waitingNode) with { Status = AgentWorkflowRunStatus.WaitingForHuman, UpdatedAt = now };
                    await runRepository.SaveAsync(run, cancellationToken);
                    await EnsureWorkItemAsync(definition, run, node, cancellationToken);
                    break;
                }

                run = await DispatchNodeAsync(definition, run, node, cancellationToken);
            }
        }

        return run;
    }

    private async Task<AgentWorkflowRun> DispatchNodeAsync(
        AgentWorkflowDefinition definition,
        AgentWorkflowRun run,
        AgentWorkflowNode node,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var current = FindNodeRun(run, node.Key);
        var queuedNode = current with
        {
            Status = AgentWorkflowNodeRunStatus.Queued,
            StartedAt = now,
            CompletedAt = null,
            AttemptCount = current.AttemptCount + 1,
            ErrorCode = null
        };
        var queuedRun = ReplaceNodeRun(run, queuedNode) with { Status = AgentWorkflowRunStatus.Running, UpdatedAt = now };
        await runRepository.SaveAsync(queuedRun, cancellationToken);

        try
        {
            AgentWorkflowReleaseOneBound.EnsureDispatchAllowed(node);
            await queueDispatcher.DispatchAsync(
                new QueueMessageEnvelope(
                    QueueName,
                    "agent-workflow.node.execute.v1",
                    run.Scope.TenantId,
                    run.Scope.WorkspaceId,
                    run.CorrelationId,
                    new AgentWorkflowNodeDispatch(
                        run.RunId,
                        definition.DefinitionId,
                        definition.Key,
                        definition.Version,
                        node.Key,
                        node.Type.ToString(),
                        node.AgentKey,
                        node.ToolKey,
                        run.BusinessReferenceType,
                        run.BusinessReferenceId,
                        queuedNode.AttemptCount)),
                cancellationToken);
            await WriteAuditAsync("agent_workflow.node.queued", node.Key, queuedNode.Status.ToString(), run.RunId.ToString(), run.BusinessReferenceId, cancellationToken, run.CorrelationId);
            return queuedRun;
        }
        catch
        {
            var failedAt = DateTimeOffset.UtcNow;
            var failedNode = queuedNode with { Status = AgentWorkflowNodeRunStatus.Failed, CompletedAt = failedAt, ErrorCode = "queue-dispatch-failed" };
            var failedRun = ReplaceNodeRun(queuedRun, failedNode) with
            {
                Status = AgentWorkflowRunStatus.Failed,
                UpdatedAt = failedAt,
                CompletedAt = failedAt,
                FailureCode = "queue-dispatch-failed"
            };
            await runRepository.SaveAsync(failedRun, cancellationToken);
            await WriteAuditAsync("agent_workflow.node.failed", node.Key, failedNode.Status.ToString(), run.RunId.ToString(), run.BusinessReferenceId, cancellationToken, run.CorrelationId);
            throw;
        }
    }

    private async Task EnsureWorkItemAsync(
        AgentWorkflowDefinition definition,
        AgentWorkflowRun run,
        AgentWorkflowNode node,
        CancellationToken cancellationToken)
    {
        var existing = await workItemRepository.GetForNodeAsync(run.RunId, node.Key, run.Scope, cancellationToken);
        if (existing?.Status is AgentWorkItemStatus.RequiresReview or AgentWorkItemStatus.InProgress or AgentWorkItemStatus.Escalated)
        {
            return;
        }

        var evidence = run.NodeRuns
            .Where(item => item.Status == AgentWorkflowNodeRunStatus.Completed)
            .SelectMany(item => item.Evidence)
            .GroupBy(item => $"{item.SourceType}:{item.SourceId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(50)
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        var workItem = new AgentWorkItem(
            Guid.NewGuid(),
            run.Scope,
            run.RunId,
            definition.Key,
            node.Key,
            node.Name,
            node.Description,
            run.BusinessReferenceType,
            run.BusinessReferenceId,
            AgentWorkItemStatus.RequiresReview,
            evidence,
            AssignedToUserId: null,
            now,
            now,
            DecidedAt: null,
            DecidedByUserId: null,
            Decision: null,
            DecisionReason: null,
            run.CorrelationId);
        await workItemRepository.SaveAsync(workItem, cancellationToken);
        await WriteAuditAsync("agent_workflow.work_item.created", node.Key, workItem.Status.ToString(), workItem.WorkItemId.ToString(), run.BusinessReferenceId, cancellationToken, run.CorrelationId);
    }

    private async Task<AgentWorkflowDefinition?> ResolveDefinitionAsync(string key, CancellationToken cancellationToken)
    {
        var system = AgentWorkflowCatalog.Find(key);
        return system ?? await definitionRepository.GetByKeyAsync(key, tenantContextAccessor.Current.Scope, cancellationToken);
    }

    private static bool IsReady(AgentWorkflowDefinition definition, AgentWorkflowRun run, AgentWorkflowNode node)
    {
        if (node.Type == AgentWorkflowNodeType.Trigger)
        {
            return true;
        }
        var incoming = definition.Edges.Where(edge => edge.TargetNodeKey.Equals(node.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (incoming.Length == 0)
        {
            return false;
        }
        if (incoming.Any(edge => FindNodeRun(run, edge.SourceNodeKey).Status is not (AgentWorkflowNodeRunStatus.Completed or AgentWorkflowNodeRunStatus.Skipped)))
        {
            return false;
        }
        return incoming.Any(edge =>
        {
            var source = FindNodeRun(run, edge.SourceNodeKey);
            return string.IsNullOrWhiteSpace(edge.Outcome)
                || string.Equals(edge.Outcome, source.Outcome, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static AgentWorkflowNodeRun FindNodeRun(AgentWorkflowRun run, string nodeKey) =>
        run.NodeRuns.First(item => item.NodeKey.Equals(nodeKey, StringComparison.OrdinalIgnoreCase));

    private static AgentWorkflowRun ReplaceNodeRun(AgentWorkflowRun run, AgentWorkflowNodeRun replacement) =>
        run with
        {
            NodeRuns = run.NodeRuns
                .Select(item => item.NodeKey.Equals(replacement.NodeKey, StringComparison.OrdinalIgnoreCase) ? replacement : item)
                .ToArray()
        };

    private async Task WriteAuditAsync(
        string action,
        string subject,
        string status,
        string targetId,
        string? evidenceId,
        CancellationToken cancellationToken,
        string? correlationId = null,
        string? provider = "AgentWorkflowEngine",
        decimal? confidence = null,
        string? auditReason = null)
    {
        var context = tenantContextAccessor.Current;
        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                context.Scope,
                CaseId: null,
                DocumentId: targetId,
                UserId: context.UserId,
                ActorType.User,
                action,
                DateTimeOffset.UtcNow,
                BeforeStatus: null,
                AfterStatus: status,
                EvidenceIds: string.IsNullOrWhiteSpace(evidenceId) ? [subject] : [subject, evidenceId],
                ModelProvider: provider,
                Confidence: confidence,
                ValidationResult: "recorded",
                ApprovalId: null,
                AuditReason: auditReason,
                CorrelationId: correlationId ?? Guid.NewGuid().ToString("N")),
            cancellationToken);
    }

    private static void ValidateDefinitionInput(
        string key,
        string name,
        IReadOnlyList<string> referenceTypes,
        IReadOnlyList<AgentWorkflowNode> nodes,
        IReadOnlyList<AgentWorkflowEdge> edges)
    {
        if (string.IsNullOrWhiteSpace(key) || !Regex.IsMatch(key.Trim(), "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
        {
            throw new AgentWorkflowValidationException(nameof(key), "Workflow key must use lowercase kebab-case.");
        }
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160)
        {
            throw new AgentWorkflowValidationException(nameof(name), "Workflow name is required (maximum 160 characters).");
        }
        if (referenceTypes.Count == 0 || referenceTypes.Any(string.IsNullOrWhiteSpace))
        {
            throw new AgentWorkflowValidationException(nameof(referenceTypes), "At least one business reference type is required.");
        }
        if (nodes.Count < 3 || nodes.Select(node => node.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != nodes.Count)
        {
            throw new AgentWorkflowValidationException(nameof(nodes), "A workflow needs at least three uniquely keyed nodes.");
        }
        if (nodes.Count(node => node.Type == AgentWorkflowNodeType.Trigger) != 1
            || nodes.All(node => node.Type != AgentWorkflowNodeType.Complete))
        {
            throw new AgentWorkflowValidationException(nameof(nodes), "A workflow needs exactly one trigger and at least one completion node.");
        }
        if (nodes.Any(node => node.SideEffect == AgentWorkflowSideEffect.ExternalSend))
        {
            throw new AgentWorkflowValidationException(nameof(nodes), "External send nodes are prohibited. Use the separately approved communication service.");
        }
        foreach (var node in nodes.Where(node => node.Type == AgentWorkflowNodeType.Agent))
        {
            if (string.IsNullOrWhiteSpace(node.AgentKey) || AgentCatalog.Find(node.AgentKey) is null)
            {
                throw new AgentWorkflowValidationException(nameof(nodes), $"Agent node '{node.Key}' references an unknown canonical agent.");
            }
        }
        foreach (var node in nodes.Where(node => node.Type is AgentWorkflowNodeType.Tool or AgentWorkflowNodeType.Decision))
        {
            if (string.IsNullOrWhiteSpace(node.ToolKey) || !AgentWorkflowToolCatalog.Tools.Contains(node.ToolKey))
            {
                throw new AgentWorkflowValidationException(nameof(nodes), $"Tool node '{node.Key}' references an unknown typed tool.");
            }
        }

        AgentWorkflowReleaseOneBound.EnsureNodesWithinAllowList(nodes);

        var keys = nodes.Select(node => node.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (edges.Count == 0 || edges.Any(edge => !keys.Contains(edge.SourceNodeKey) || !keys.Contains(edge.TargetNodeKey)))
        {
            throw new AgentWorkflowValidationException(nameof(edges), "Every edge must connect existing nodes.");
        }

        AgentWorkflowHumanReviewInvariant.Ensure(nodes, edges);
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AgentWorkflowNodeDispatch(
    Guid RunId,
    Guid DefinitionId,
    string WorkflowKey,
    int WorkflowVersion,
    string NodeKey,
    string NodeType,
    string? AgentKey,
    string? ToolKey,
    string BusinessReferenceType,
    string BusinessReferenceId,
    int Attempt);
