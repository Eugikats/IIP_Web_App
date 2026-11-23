using System.Text.Json;
using System.Text.Json.Nodes;

namespace InsuranceIntelligence.Application.Voice;

/// <summary>
/// Where live voice runs. No key lives here: the API reaches Voice Live as its own managed identity.
/// Voice is "not configured" until <see cref="Endpoint"/> is set, and the portal says so.
/// </summary>
public sealed class VoiceLiveOptions
{
    /// <summary>The resource, for example https://ais-iip-dev-xxxx.services.ai.azure.com. Empty means live voice is off.</summary>
    public string? Endpoint { get; init; }

    public string Model { get; init; } = "gpt-realtime-mini";

    public string ApiVersion { get; init; } = "2026-07-15";

    public string Voice { get; init; } = "alloy";

    /// <summary>Where the speech is processed, said to the person before their first call.</summary>
    public string ProcessingRegion { get; init; } = "Sweden";

    /// <summary>Quiet this long ends the person's turn. Shorter cuts people off mid-sentence.</summary>
    public int SilenceMilliseconds { get; init; } = 500;

    /// <summary>A call with nothing said for this long is closed.</summary>
    public int IdleSeconds { get; init; } = 180;

    /// <summary>
    /// The same resource's speech address, used for the short "Hey Kato" check. When it is not set it is taken from
    /// <see cref="Endpoint"/> (services.ai.azure.com becomes cognitiveservices.azure.com).
    /// </summary>
    public string? SpeechEndpoint { get; init; }

    public string? ResolvedSpeechEndpoint =>
        !string.IsNullOrWhiteSpace(SpeechEndpoint)
            ? SpeechEndpoint!.TrimEnd('/')
            : string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint!.TrimEnd('/').Replace(".services.ai.azure.com", ".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}

/// <summary>One link to Voice Live for one call. Tests script this; production uses a WebSocket.</summary>
public interface IVoiceLiveTransport : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);

    Task SendAsync(string json, CancellationToken cancellationToken);

    /// <summary>The next message, or null when the other side has closed.</summary>
    Task<string?> ReceiveAsync(CancellationToken cancellationToken);
}

public interface IVoiceLiveTransportFactory
{
    IVoiceLiveTransport Create();
}

/// <summary>
/// The messages of the Voice Live control socket in one place. The shapes follow the realtime protocol
/// (checked against the live service on 2026-11-23 for session.update, function calls and their results);
/// the call-setup pair <c>rtc.call.sdp.create</c> and <c>rtc.call.sdp.created</c> is from the service
/// documentation and is proved by the first real call.
/// </summary>
public static class VoiceLiveProtocol
{
    public static string CreateCall(string sdpOffer, JsonObject session) =>
        new JsonObject { ["type"] = "rtc.call.sdp.create", ["sdp_offer"] = sdpOffer, ["session"] = session }.ToJsonString();

    public static string FunctionResult(string callId, string outputJson) =>
        new JsonObject
        {
            ["type"] = "conversation.item.create",
            ["item"] = new JsonObject { ["type"] = "function_call_output", ["call_id"] = callId, ["output"] = outputJson },
        }.ToJsonString();

    public static string ResponseCreate() => new JsonObject { ["type"] = "response.create" }.ToJsonString();

    /// <summary>The model is told, as the system, what has changed (for example that the phone is now locked).</summary>
    public static string SystemNote(string text) =>
        new JsonObject
        {
            ["type"] = "conversation.item.create",
            ["item"] = new JsonObject
            {
                ["type"] = "message",
                ["role"] = "system",
                ["content"] = new JsonArray(new JsonObject { ["type"] = "input_text", ["text"] = text }),
            },
        }.ToJsonString();

    public static JsonObject SessionSettings(VoiceLiveOptions options, string instructions, JsonArray tools) =>
        new()
        {
            ["instructions"] = instructions,
            ["voice"] = new JsonObject { ["name"] = options.Voice, ["type"] = "openai" },
            ["turn_detection"] = new JsonObject
            {
                ["type"] = "azure_semantic_vad",
                ["silence_duration_ms"] = options.SilenceMilliseconds,
                ["interrupt_response"] = true,
            },
            ["input_audio_noise_reduction"] = new JsonObject { ["type"] = "azure_deep_noise_suppression" },
            ["input_audio_echo_cancellation"] = new JsonObject { ["type"] = "server_echo_cancellation" },
            ["tools"] = tools,
            ["tool_choice"] = "auto",
        };

    public static VoiceLiveMessage Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return new VoiceLiveMessage(Text("type") ?? string.Empty, Text("call_id"), Text("name"), Text("arguments"), Text("sdp_answer"),
                root.TryGetProperty("error", out var error) ? (error.TryGetProperty("message", out var message) ? message.GetString() : error.ToString()) : null);
        }
        catch (JsonException)
        {
            return new VoiceLiveMessage(string.Empty, null, null, null, null, null);
        }
    }
}

public sealed record VoiceLiveMessage(string Type, string? CallId, string? Name, string? Arguments, string? SdpAnswer, string? Error)
{
    public bool IsFunctionCall => Type == "response.function_call_arguments.done" && CallId is not null && Name is not null;
}
