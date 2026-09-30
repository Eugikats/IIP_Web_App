using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Infrastructure.Translation;

/// <summary>
/// Tenant-supplied settings for Sunbird AI (docs.sunbird.ai), parsed defensively from the flat
/// connection settings dictionary the same way EgoSmsSettings.TryParse is: never throws on a
/// malformed or missing field, callers get a clear error string instead.
/// </summary>
public sealed record SunbirdSettings(string ApiKey, string Endpoint, string Model, int RequestTimeoutSeconds)
{
    public const string DefaultEndpoint = "https://api.sunbird.ai";

    /// <summary>Sunbird's broader-language default per its own docs (67 languages vs sunflower-14b's
    /// 32); changeable per tenant from the dashboard without a redeploy.</summary>
    public const string DefaultModel = "sunflower-9b";

    public static bool TryParse(IReadOnlyDictionary<string, string> settings, out SunbirdSettings? parsed, out string? error)
    {
        parsed = null;
        if (!settings.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            error = "A Sunbird AI API key is required.";
            return false;
        }

        var endpoint = settings.TryGetValue("endpoint", out var explicitEndpoint) && !string.IsNullOrWhiteSpace(explicitEndpoint)
            ? explicitEndpoint.Trim().TrimEnd('/')
            : DefaultEndpoint;
        var model = settings.TryGetValue("model", out var explicitModel) && !string.IsNullOrWhiteSpace(explicitModel)
            ? explicitModel.Trim()
            : DefaultModel;
        var timeoutSeconds = settings.TryGetValue("requestTimeoutSeconds", out var timeoutRaw) && int.TryParse(timeoutRaw, out var parsedTimeout)
            ? parsedTimeout
            : 60;

        parsed = new SunbirdSettings(apiKey.Trim(), endpoint, model, timeoutSeconds);
        error = null;
        return true;
    }
}

/// <summary>
/// Calls Sunbird AI's OpenAI-compatible chat/completions endpoint (sunflower-9b/14b), not its
/// purpose-built /tasks/translate endpoint: that endpoint's request schema has no field for a
/// system prompt, so Sunbird's own internal instruction drives it and cannot be inspected or
/// changed. chat/completions accepts a system message, which is the only lever this platform has
/// to enforce verbatim-only translation (see BuildSystemPrompt) rather than trusting a black box.
///
/// Like AfriSlmTranslationProvider, this is a translation model call, not an assistant: nothing it
/// returns is trusted on its own. TranslationService judges every output and falls back to English
/// when it is bad; this class only builds the request and reads the reply.
/// </summary>
public sealed class SunbirdTranslationProvider(SunbirdSettings settings, HttpClient httpClient)
    : ITranslationProvider, ITranslationEngineHealth
{
    public const int MinTokens = 64;
    public const int MaxTokens = 512;

    /// <summary>A connection is only ever constructed from settings that already parsed (see
    /// SunbirdSettings.TryParse and SunbirdConnectionValidator), so an instance of this class is
    /// always configured; "not configured" is TenantAwareTranslationProvider's concern, before this
    /// class is ever built.</summary>
    public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult($"sunbird:{settings.Model}");

    public async Task<string> TranslateAsync(
        string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url("/tasks/chat/completions"))
        {
            Content = new StringContent(BuildRequestBody(text, from, to, settings.Model), Encoding.UTF8, "application/json"),
        };
        Authorize(request);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 120)));
        using var response = await httpClient.SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        return ReadContent(document.RootElement);
    }

    /// <summary>Sunbird documents no dedicated /health route the way the AfriSLM container does, so
    /// this uses the cheapest real endpoint its quickstart documents (language identification on a
    /// short fixed string) rather than claim healthy without checking anything real.</summary>
    public async Task<TranslationEngineStatus> CheckAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Url("/tasks/language_id"))
            {
                Content = new StringContent("""{"text":"hello"}""", Encoding.UTF8, "application/json"),
            };
            Authorize(request);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode ? TranslationEngineStatus.Healthy : TranslationEngineStatus.Failing;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return TranslationEngineStatus.Failing;
        }
    }

    /// <summary>Explicit and restrictive: this is the actual point of the requirement that Sunbird
    /// must translate verbatim. Deliberately drops AfriSLM's "convey meaning and nuances... cultural
    /// sensitivities" framing, which invites adaptation rather than constraining to it, and calls
    /// out the specific ways an instruction-tuned chat model drifts from a literal translation
    /// (paraphrasing, answering the text instead of translating it, "helpfully" correcting it).</summary>
    public static string BuildSystemPrompt(string from, string to) =>
        $"You are a literal, verbatim translation engine from {from} to {to}. Output ONLY the direct " +
        "translation of the input text, with nothing else. Do not paraphrase, do not summarize, do not " +
        "add information, do not remove information, do not correct or improve the source text, do not " +
        "add explanations, greetings, or commentary, and do not answer the text as if it were addressed " +
        "to you. If the input is a question, translate the question, do not answer it. If the input " +
        "names an amount, date, ID, or number, reproduce it unchanged. Copy every placeholder in square " +
        "brackets, such as [TKN0], exactly as written and in the same place. Preserve the tone and register " +
        "of the source exactly; do not soften, formalize, or localize it. Your entire response must be " +
        "the translation and nothing more.";

    /// <summary>Wraps the source in a fixed delimiter so the model cannot confuse instruction and
    /// content, unlike AfriSLM's free-form "translate the following text: {text}", which has no such
    /// separation and gives an instruction-tuned model more room to respond to the text rather than
    /// translate it.</summary>
    public static string BuildUserPrompt(string from, string to, string text) =>
        $"Text to translate from {from} to {to} (between the markers, translate only what is between " +
        $"them):\n<<<\n{text}\n>>>";

    /// <summary>Words times three plus 32, held between 64 and 512, the same non-failing-model risk
    /// AfriSlmTranslationProvider.TokenBudget guards against: a model that does not fail loudly can
    /// loop a phrase until the budget runs out, so the budget is the only hard stop.</summary>
    public static int TokenBudget(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Clamp((words * 3) + 32, MinTokens, MaxTokens);
    }

    /// <summary>Full language names from the Domain's own catalog, independent of AfriSLM's tuned
    /// naming (which deliberately says "Swahili" rather than "Kiswahili" to match its own model
    /// card); Sunbird is a different model with no such documented preference.</summary>
    public static string PromptName(SupportedLanguageCode code) => SupportedLanguage.For(code).DisplayName;

    public static string BuildRequestBody(string text, SupportedLanguageCode from, SupportedLanguageCode to, string model)
    {
        var fromName = PromptName(from);
        var toName = PromptName(to);
        return JsonSerializer.Serialize(new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt(fromName, toName) },
                new { role = "user", content = BuildUserPrompt(fromName, toName, text) },
            },
            temperature = 0,
            max_tokens = TokenBudget(text),
            stream = false,
        });
    }

    public static string ReadContent(JsonElement root)
    {
        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private string Url(string path) => settings.Endpoint.TrimEnd('/') + path;

    private void Authorize(HttpRequestMessage request) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
}

/// <summary>
/// Validates a Sunbird AI connection before anything is persisted, the same guarantee
/// EgoSmsConnectionValidator/AfricasTalkingConnectionValidator give SMS. It sends one real, short
/// translation through the exact request the provider uses (same endpoint, model and verbatim
/// prompt), so a wrong endpoint or model name fails here rather than on a customer's message, and
/// the sample output is shown on the connections screen for a person to read.
/// </summary>
public sealed class SunbirdConnectionValidator(IHttpClientFactory httpClientFactory) : IConnectionValidator
{
    public const string SampleText = "Please send photos of the damage to your car.";

    private readonly HttpClient httpClient = httpClientFactory.CreateClient(nameof(SunbirdConnectionValidator));

    public ConnectionType ConnectionType => ConnectionType.Translation;
    public string ProviderKey => "Sunbird";

    public async Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        if (!SunbirdSettings.TryParse(settings, out var parsed, out var parseError) || parsed is null)
        {
            return new ConnectionValidationResult(false, parseError);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{parsed.Endpoint}/tasks/chat/completions")
            {
                Content = new StringContent(
                    SunbirdTranslationProvider.BuildRequestBody(SampleText, SupportedLanguageCode.En, SupportedLanguageCode.Lg, parsed.Model),
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parsed.ApiKey);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(parsed.RequestTimeoutSeconds, 5, 120)));
            using var response = await httpClient.SendAsync(request, timeout.Token);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                return new ConnectionValidationResult(false, "Sunbird AI rejected this API key. Verify it at sunbird.ai.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ConnectionValidationResult(false, "Sunbird AI did not accept the request. Check the endpoint and model name.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var sample = TranslationQualityJudge.Clean(SunbirdTranslationProvider.ReadContent(document.RootElement));
            if (string.IsNullOrWhiteSpace(sample))
            {
                return new ConnectionValidationResult(false, "Sunbird AI answered but returned no translation. Check the model name.");
            }

            return new ConnectionValidationResult(true, null, new Dictionary<string, string>
            {
                ["model"] = parsed.Model,
                ["sample (English to Luganda)"] = sample,
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ConnectionValidationResult(false, "Could not reach Sunbird AI at this endpoint.");
        }
    }
}
