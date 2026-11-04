using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Application.Billing;

// Every call to a model goes through one of these three wrappers, so no feature has to remember to count its own
// usage and a new feature cannot forget. Each records what the call used against the workspace that caused it, and
// otherwise passes the call and its result through untouched.

/// <summary>Meters the single-prompt chat model (summaries, drafts, assessments, wording extraction).</summary>
public sealed class MeteredChatCompletionProvider(
    IChatCompletionProvider inner,
    IWorkspaceAiUsageRecorder recorder,
    ITenantContextAccessor tenantContext) : IChatCompletionProvider
{
    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.CompleteAsync(request, cancellationToken);
        var scope = ScopeFor(request.TenantId, request.WorkspaceId, tenantContext);
        if (scope is not null)
        {
            await recorder.RecordAsync(
                new WorkspaceAiUsageEntry(scope, $"chat:{request.AgentKey ?? "general"}", result.Provider, result.Deployment, result.ModelVersion, result.PromptTokens, result.CompletionTokens, request.CorrelationId),
                cancellationToken);
        }

        return result;
    }

    internal static TenantScope? ScopeFor(string? tenantId, string? workspaceId, ITenantContextAccessor accessor)
    {
        if (accessor.HasCurrent && (string.IsNullOrWhiteSpace(tenantId) || string.Equals(accessor.Current.TenantId, tenantId, StringComparison.Ordinal)))
        {
            return accessor.Current.Scope;
        }

        // A caller outside a request (a background job) names its workspace on the request itself.
        return string.IsNullOrWhiteSpace(tenantId) ? null : TenantScope.Create(tenantId, string.IsNullOrWhiteSpace(workspaceId) ? tenantId : workspaceId);
    }
}

/// <summary>Meters the model that calls tools (the WhatsApp assistant and the conversation agents), one record per step.</summary>
public sealed class MeteredToolCallingChatProvider(
    IToolCallingChatProvider inner,
    IWorkspaceAiUsageRecorder recorder,
    ITenantContextAccessor tenantContext) : IToolCallingChatProvider
{
    public async Task<AgentChatStep> NextAsync(
        IReadOnlyList<AgentChatMessage> messages,
        IReadOnlyList<AgentToolSpec> tools,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var step = await inner.NextAsync(messages, tools, correlationId, cancellationToken);
        if (tenantContext.HasCurrent)
        {
            var usage = step.Usage;
            await recorder.RecordAsync(
                new WorkspaceAiUsageEntry(tenantContext.Current.Scope, "agent-chat", usage?.Provider ?? "Unknown", usage?.Deployment, usage?.ModelVersion, usage?.PromptTokens, usage?.CompletionTokens, correlationId),
                cancellationToken);
        }

        return step;
    }
}

/// <summary>Meters embeddings (turning text into vectors for search).</summary>
public sealed class MeteredEmbeddingProvider(
    IEmbeddingProvider inner,
    IWorkspaceAiUsageRecorder recorder,
    ITenantContextAccessor tenantContext) : IEmbeddingProvider
{
    public async Task<IReadOnlyCollection<float>> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
        (await EmbedWithUsageAsync(text, cancellationToken)).Vector;

    public async Task<EmbeddingResult> EmbedWithUsageAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await inner.EmbedWithUsageAsync(text, cancellationToken);
        if (tenantContext.HasCurrent && result.Usage is { } usage)
        {
            await recorder.RecordAsync(
                new WorkspaceAiUsageEntry(tenantContext.Current.Scope, "embedding", usage.Provider, usage.Deployment, usage.ModelVersion, usage.PromptTokens, usage.CompletionTokens, null),
                cancellationToken);
        }

        return result;
    }
}
