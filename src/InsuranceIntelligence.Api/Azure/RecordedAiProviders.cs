using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Azure;

// Every AI call goes through one of these. Before the call they stop it if the organisation's credits for the
// month are used up; after it they record the real token counts and what the call was worth. They add nothing
// to the call itself and keep no prompt or answer text.

public sealed class RecordedChatCompletionProvider(IChatCompletionProvider inner, IAiUsageRecorder recorder) : IChatCompletionProvider
{
    // The agent that asked names the feature; a call that names none is counted under whoever started the work.
    private static string? FeatureFor(string? agentKey) => agentKey switch
    {
        null or "" => null,
        "customer-update-drafting" => "customer_updates",
        "report-insight" or "command-center" => "command_center",
        "policy-intelligence" => "policy_answers",
        var other => other.Replace('-', '_'),
    };

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        await recorder.EnsureCreditsAsync(cancellationToken);
        var result = await inner.CompleteAsync(request, cancellationToken);
        await recorder.RecordAsync(result.Provider, result.Deployment ?? result.ModelVersion, result.PromptTokens, result.CompletionTokens, FeatureFor(request.AgentKey), cancellationToken);
        return result;
    }
}

public sealed class RecordedToolCallingChatProvider(IToolCallingChatProvider inner, IAiUsageRecorder recorder) : IToolCallingChatProvider
{
    public async Task<AgentChatStep> NextAsync(
        IReadOnlyList<AgentChatMessage> messages,
        IReadOnlyList<AgentToolSpec> tools,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        await recorder.EnsureCreditsAsync(cancellationToken);
        var step = await inner.NextAsync(messages, tools, correlationId, cancellationToken);
        await recorder.RecordAsync("AzureOpenAI", step.Model, step.PromptTokens, step.CompletionTokens, null, cancellationToken);
        return step;
    }
}

public sealed class RecordedAiAssistantProvider(IAiAssistantProvider inner, IAiUsageRecorder recorder) : IAiAssistantProvider
{
    public async Task<AssistantAnswer> AnswerPolicyQuestionAsync(
        AssistantQuestion question,
        RetrievalResult retrievalResult,
        CancellationToken cancellationToken = default)
    {
        await recorder.EnsureCreditsAsync(cancellationToken);
        var answer = await inner.AnswerPolicyQuestionAsync(question, retrievalResult, cancellationToken);
        await recorder.RecordAsync(answer.ModelProvider, answer.ModelDeployment ?? answer.ModelVersion, answer.PromptTokens, answer.CompletionTokens, "policy_answers", cancellationToken);
        return answer;
    }
}
