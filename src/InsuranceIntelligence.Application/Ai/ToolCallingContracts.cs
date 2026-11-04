namespace InsuranceIntelligence.Application.Ai;

/// <summary>A function the model may ask to run. <see cref="Parameters"/> is a JSON Schema object,
/// serialised as given.</summary>
public sealed record AgentToolSpec(string Name, string Description, object Parameters);

public sealed record AgentToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>One message in a tool-calling conversation: "system", "user", "assistant" (with
/// optional tool calls) or "tool" (the result of one call, tied to it by <see cref="ToolCallId"/>).</summary>
public sealed record AgentChatMessage(
    string Role,
    string? Content,
    IReadOnlyList<AgentToolCall>? ToolCalls = null,
    string? ToolCallId = null)
{
    public static AgentChatMessage System(string content) => new("system", content);
    public static AgentChatMessage User(string content) => new("user", content);
    public static AgentChatMessage Assistant(string? content, IReadOnlyList<AgentToolCall>? toolCalls = null) => new("assistant", content, toolCalls);
    public static AgentChatMessage Tool(string toolCallId, string content) => new("tool", content, ToolCallId: toolCallId);
}

/// <summary>What the model did next: asked for tools to be run, or answered.</summary>
public sealed record AgentChatStep(string? Content, IReadOnlyList<AgentToolCall> ToolCalls, InsuranceIntelligence.Domain.Knowledge.ModelUsage? Usage = null)
{
    public int? PromptTokens => Usage?.PromptTokens;
    public int? CompletionTokens => Usage?.CompletionTokens;
    public string? Model => Usage?.ModelVersion;

    public AgentChatStep(string? content, IReadOnlyList<AgentToolCall> toolCalls, int? promptTokens, int? completionTokens, string? model)
        : this(content, toolCalls, new InsuranceIntelligence.Domain.Knowledge.ModelUsage(
            "AzureOpenAI", null, model, promptTokens, completionTokens))
    {
    }
}

/// <summary>A chat model that can call tools. Separate from <see cref="IChatCompletionProvider"/>,
/// which takes one prompt and returns one answer.</summary>
public interface IToolCallingChatProvider
{
    Task<AgentChatStep> NextAsync(
        IReadOnlyList<AgentChatMessage> messages,
        IReadOnlyList<AgentToolSpec> tools,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
