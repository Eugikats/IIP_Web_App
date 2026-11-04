using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

/// <summary>
/// Azure OpenAI chat completions with function calling, for Kato. The caller supplies the whole
/// system prompt, including the platform's non-negotiable preamble, and runs the tools; this class
/// only carries the conversation to the model and back.
/// </summary>
public sealed class AzureOpenAiToolCallingChatProvider(
    InfrastructureOptions options,
    ILogger<AzureOpenAiToolCallingChatProvider> logger,
    HttpClient? httpClient = null,
    TokenCredential? tokenCredential = null) : IToolCallingChatProvider
{
    private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";
    private readonly HttpClient httpClient = httpClient ?? new HttpClient();
    private readonly TokenCredential tokenCredential = tokenCredential ?? AzureProviderSupport.Credential();

    public async Task<AgentChatStep> NextAsync(
        IReadOnlyList<AgentChatMessage> messages,
        IReadOnlyList<AgentToolSpec> tools,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (AzureProviderSupport.Missing(options.AzureOpenAI.Endpoint) || AzureProviderSupport.Missing(options.AzureOpenAI.ChatDeployment))
        {
            throw new InvalidOperationException("Azure OpenAI is not configured for tool calling.");
        }

        var body = BuildRequestBody(messages, tools, options.AzureOpenAI.AgentReasoningEffort);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.AzureOpenAI.Endpoint!.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(options.AzureOpenAI.ChatDeployment!)}/chat/completions?api-version={Uri.EscapeDataString(options.AzureOpenAI.ApiVersion)}")
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };

        var token = await tokenCredential.GetTokenAsync(new TokenRequestContext([CognitiveServicesScope]), cancellationToken);
        request.Headers.Authorization = new("Bearer", token.Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.AzureOpenAI.RequestTimeoutSeconds * 2, 45, 120)));
        using var response = await httpClient.SendAsync(request, timeout.Token);
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Azure OpenAI tool-calling request failed with HTTP {StatusCode}. CorrelationId={CorrelationId}", (int)response.StatusCode, correlationId);
            throw new InvalidOperationException($"Azure OpenAI returned HTTP {(int)response.StatusCode}.");
        }

        var step = ParseStep(json);
        return step.Usage is null ? step : step with { Usage = step.Usage with { Deployment = options.AzureOpenAI.ChatDeployment } };
    }

    public static JsonObject BuildRequestBody(IReadOnlyList<AgentChatMessage> messages, IReadOnlyList<AgentToolSpec> tools, string? reasoningEffort = null)
    {
        var messageArray = new JsonArray();
        foreach (var message in messages)
        {
            var node = new JsonObject { ["role"] = message.Role };
            node["content"] = message.Content is null ? null : JsonValue.Create(message.Content);
            if (message.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var call in message.ToolCalls)
                {
                    calls.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.ArgumentsJson },
                    });
                }

                node["tool_calls"] = calls;
            }

            if (message.ToolCallId is not null)
            {
                node["tool_call_id"] = message.ToolCallId;
            }

            messageArray.Add(node);
        }

        var body = new JsonObject
        {
            ["messages"] = messageArray,
            // Room for the model's own reasoning as well as a short reply.
            ["max_completion_tokens"] = 4000,
        };

        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            body["reasoning_effort"] = reasoningEffort.Trim().ToLowerInvariant();
        }

        if (tools.Count > 0)
        {
            var toolArray = new JsonArray();
            foreach (var tool in tools)
            {
                toolArray.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = JsonSerializer.SerializeToNode(tool.Parameters),
                    },
                });
            }

            body["tools"] = toolArray;
            body["tool_choice"] = "auto";
        }

        return body;
    }

    public static AgentChatStep ParseStep(string json)
    {
        using var document = JsonDocument.Parse(json);
        var message = document.RootElement.GetProperty("choices")[0].GetProperty("message");
        var content = message.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String
            ? InsuranceIntelligence.Domain.Common.DisplayText.WithoutEmDashes(contentElement.GetString()!)
            : null;

        var calls = new List<AgentToolCall>();
        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                var function = call.GetProperty("function");
                calls.Add(new AgentToolCall(
                    call.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                    function.GetProperty("name").GetString() ?? string.Empty,
                    function.TryGetProperty("arguments", out var arguments) ? arguments.GetString() ?? "{}" : "{}"));
            }
        }

        InsuranceIntelligence.Domain.Knowledge.ModelUsage? usage = null;
        if (document.RootElement.TryGetProperty("usage", out var usageElement) && usageElement.ValueKind == JsonValueKind.Object)
        {
            usage = new InsuranceIntelligence.Domain.Knowledge.ModelUsage(
                "AzureOpenAI",
                null,
                document.RootElement.TryGetProperty("model", out var modelElement) && modelElement.ValueKind == JsonValueKind.String ? modelElement.GetString() : null,
                usageElement.TryGetProperty("prompt_tokens", out var promptElement) && promptElement.TryGetInt32(out var promptTokens) ? promptTokens : null,
                usageElement.TryGetProperty("completion_tokens", out var completionElement) && completionElement.TryGetInt32(out var completionTokens) ? completionTokens : null);
        }

        return new AgentChatStep(content, calls, usage);
    }
}
