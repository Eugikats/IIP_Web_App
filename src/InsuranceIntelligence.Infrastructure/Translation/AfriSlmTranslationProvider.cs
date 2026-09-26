using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Translation;

/// <summary>
/// Calls the AfriSLM model (TranslatePsy-AfriSLM, a 0.8B model in 4-bit) served by a llama.cpp
/// server in its own container. It is a translation model, not an assistant: it has no notion of
/// what a customer may be told, so nothing it returns is trusted. <see cref="TranslationService"/>
/// judges every output and falls back to English when it is bad, and this class only builds the
/// request the model was tuned on and reads the reply.
///
/// The prompt, the language names and the token budget are the ones the model card and the
/// reference translation app document. The model holds its shape only near that distribution, so
/// they are deliberately not paraphrased: "Swahili", not "Kiswahili", and the trailing full stop
/// after the text is part of the format.
/// </summary>
public sealed class AfriSlmTranslationProvider(TranslationOptions options, HttpClient httpClient)
    : ITranslationProvider, ITranslationEngineHealth
{
    public const int MinTokens = 64;
    public const int MaxTokens = 512;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Endpoint);

    public async Task<string> TranslateAsync(
        string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("The translation engine is not configured.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, Url("/v1/chat/completions"))
        {
            Content = new StringContent(BuildRequestBody(text, from, to), Encoding.UTF8, "application/json"),
        };
        Authorize(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ReadContent(document.RootElement);
    }

    public async Task<TranslationEngineStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return TranslationEngineStatus.NotConfigured;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Url("/health"));
            Authorize(request);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode ? TranslationEngineStatus.Healthy : TranslationEngineStatus.Failing;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return TranslationEngineStatus.Failing;
        }
    }

    public static string BuildSystemPrompt(string from, string to) =>
        $"You are a professional {from} to {to} translator. Your goal is to accurately convey the meaning and " +
        $"nuances of the original {from} text while adhering to {to} grammar, vocabulary, and cultural " +
        $"sensitivities. Produce only the {to} translation, without any additional explanations or commentary.";

    public static string BuildUserPrompt(string from, string to, string text) =>
        $"Please translate the following {from} text into {to}: {text}.\n\nTranslation:";

    /// <summary>Words times three plus 32, held between 64 and 512. The model does not fail loudly
    /// and can loop a phrase until the budget runs out, so the budget is the only hard stop.</summary>
    public static int TokenBudget(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Clamp((words * 3) + 32, MinTokens, MaxTokens);
    }

    public static string PromptName(SupportedLanguageCode code) => code switch
    {
        SupportedLanguageCode.En => "English",
        SupportedLanguageCode.Lg => "Luganda",
        SupportedLanguageCode.Sw => "Swahili",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "AfriSLM has no prompt name for this language."),
    };

    public static string BuildRequestBody(string text, SupportedLanguageCode from, SupportedLanguageCode to)
    {
        var fromName = PromptName(from);
        var toName = PromptName(to);
        return JsonSerializer.Serialize(new
        {
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

    private string Url(string path) => options.Endpoint!.TrimEnd('/') + path;

    private void Authorize(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }
}
