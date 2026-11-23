using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Identity;
using InsuranceIntelligence.Application.Voice;

namespace InsuranceIntelligence.Api.Voice;

/// <summary>
/// Writes down a two or three second clip with Azure Speech fast transcription, as the API's own managed identity.
/// One kept-alive HTTP connection is reused: a fresh secure connection costs about two seconds and a reused one about
/// four tenths of one, and the phone is waiting.
/// </summary>
public sealed class AzureSpeechWakeChecker(VoiceLiveOptions options, TokenCredential credential, HttpClient http) : IWakeChecker
{
    private const string Scope = "https://cognitiveservices.azure.com/.default";
    private const string ApiVersion = "2024-11-15";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ResolvedSpeechEndpoint);

    public async Task<WakeCheck?> CheckAsync(byte[] wav, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([Scope]), cancellationToken);
            using var form = new MultipartFormDataContent();
            var audio = new ByteArrayContent(wav);
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(audio, "audio", "wake.wav");
            form.Add(new StringContent("{\"locales\":[\"en-US\"]}", System.Text.Encoding.UTF8, "application/json"), "definition");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ResolvedSpeechEndpoint}/speechtotext/transcriptions:transcribe?api-version={ApiVersion}") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var heard = body.RootElement.TryGetProperty("combinedPhrases", out var phrases) && phrases.ValueKind == JsonValueKind.Array
                ? string.Join(' ', phrases.EnumerateArray().Select(phrase => phrase.TryGetProperty("text", out var text) ? text.GetString() : null).Where(text => !string.IsNullOrWhiteSpace(text)))
                : string.Empty;
            return new WakeCheck(WakePhrase.Matches(heard), heard.Length > 120 ? heard[..120] : heard);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or AuthenticationFailedException or OperationCanceledException)
        {
            return null;
        }
    }
}
