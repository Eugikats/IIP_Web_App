using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

/// <summary>
/// Recognises documents with an Azure OpenAI vision model (build-loop item 14): the picture is
/// shrunk to a JPEG preview, shown to the model with the workspace's list of document types, and the
/// model answers with a type, a confidence and a reason in one line. A PDF is not a picture, so it is
/// reported as "a person will look" rather than guessed at. The model only advises: the caller never
/// moves a customer's own choice because of it, and anything below the confident threshold goes to
/// a person.
/// </summary>
public sealed class AzureOpenAiVisionDocumentRecognizer(
    InfrastructureOptions options,
    IImageDerivativeProvider derivativeProvider,
    ILogger<AzureOpenAiVisionDocumentRecognizer> logger,
    HttpClient? httpClient = null,
    TokenCredential? tokenCredential = null) : IDocumentRecognizer
{
    public const string ProviderName = "AzureOpenAI";
    private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";
    private const int ImageMaxEdge = 1280;
    private readonly HttpClient httpClient = httpClient ?? new HttpClient();
    private readonly TokenCredential tokenCredential = tokenCredential ?? AzureProviderSupport.Credential();

    public async Task<DocumentRecognition> RecognizeAsync(DocumentRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        if (AzureProviderSupport.Missing(options.AzureOpenAI.Endpoint) || AzureProviderSupport.Missing(options.AzureOpenAI.ChatDeployment))
        {
            throw new InvalidOperationException("Azure OpenAI document recognition is selected but not configured.");
        }

        ImageDerivative? picture;
        await using (var stream = await request.OpenAsync(cancellationToken))
        {
            picture = await derivativeProvider.CreatePreviewAsync(stream, request.FileName, ImageMaxEdge, cancellationToken);
        }

        if (picture is null)
        {
            return new DocumentRecognition(null, 0m, "Only pictures can be read automatically for now, so a person will look at this file.", ProviderName, null, DateTimeOffset.UtcNow);
        }

        var catalogue = string.Join("\n", request.Catalogue.Select(entry => $"- {entry.Key}: {entry.Label}. {entry.Description}"));
        var instructions =
            "Which of these document types is the picture? Choose only from this list, using the key before the colon.\n"
            + catalogue
            + "\nAnswer with one JSON object and nothing else: {\"typeKey\": string or null, \"confidence\": number from 0 to 1, \"reason\": one short sentence saying what on the page tells you}. "
            + "Use null if it is none of them or you cannot tell. Do not decide anything about a claim.";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Uri())
        {
            Content = JsonContent.Create(new
            {
                messages = new object[]
                {
                    new { role = "system", content = "You identify insurance claim documents from a photo. You only classify; people decide." },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = instructions },
                            new { type = "image_url", image_url = new { url = $"data:{picture.ContentType};base64,{Convert.ToBase64String(picture.Content)}" } },
                        },
                    },
                },
                max_completion_tokens = 200,
            }),
        };
        var token = await tokenCredential.GetTokenAsync(new TokenRequestContext([CognitiveServicesScope]), cancellationToken);
        httpRequest.Headers.Authorization = new("Bearer", token.Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.AzureOpenAI.RequestTimeoutSeconds, 5, 120)));
        using var response = await httpClient.SendAsync(httpRequest, timeout.Token);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Azure OpenAI response was empty.");
        var content = payload.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
        return Parse(content, request.Catalogue, payload.Model);
    }

    /// <summary>Reads the model's one-line answer. Anything that is not the expected shape becomes
    /// "could not tell", never an error and never a guess.</summary>
    public static DocumentRecognition Parse(string content, IReadOnlyList<DocumentCatalogueEntry> catalogue, string? model)
    {
        var now = DateTimeOffset.UtcNow;
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return new DocumentRecognition(null, 0m, "The answer could not be read.", ProviderName, model, now);
        }

        try
        {
            using var document = JsonDocument.Parse(content[start..(end + 1)]);
            var root = document.RootElement;
            var key = root.TryGetProperty("typeKey", out var keyElement) && keyElement.ValueKind == JsonValueKind.String ? keyElement.GetString() : null;
            var confidence = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDecimal(out var number) ? Math.Clamp(number, 0m, 1m) : 0m;
            var reason = root.TryGetProperty("reason", out var reasonElement) && reasonElement.ValueKind == JsonValueKind.String ? reasonElement.GetString() ?? string.Empty : string.Empty;
            var entry = key is null ? null : catalogue.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
            return entry is null
                ? new DocumentRecognition(null, 0m, string.IsNullOrWhiteSpace(reason) ? "It does not match a known type of document." : reason, ProviderName, model, now)
                : new DocumentRecognition(entry.Key, confidence, string.IsNullOrWhiteSpace(reason) ? $"It looks like a {entry.Label.ToLowerInvariant()}." : InsuranceIntelligence.Domain.Common.DisplayText.WithoutEmDashes(reason), ProviderName, model, now);
        }
        catch (JsonException)
        {
            return new DocumentRecognition(null, 0m, "The answer could not be read.", ProviderName, model, now);
        }
    }

    private Uri Uri() =>
        new($"{options.AzureOpenAI.Endpoint!.TrimEnd('/')}/openai/deployments/{System.Uri.EscapeDataString(options.AzureOpenAI.ChatDeployment!)}/chat/completions?api-version={System.Uri.EscapeDataString(options.AzureOpenAI.ApiVersion)}");
}

/// <summary>Used where no recognition model is configured: every file honestly goes to a person
/// (or stays where the customer put it) rather than being guessed at.</summary>
public sealed class NotConfiguredDocumentRecognizer : IDocumentRecognizer
{
    public Task<DocumentRecognition> RecognizeAsync(DocumentRecognitionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DocumentRecognition(null, 0m, "Automatic recognition is not configured, so a person will look at this file.", "NotConfigured", null, DateTimeOffset.UtcNow));
}
