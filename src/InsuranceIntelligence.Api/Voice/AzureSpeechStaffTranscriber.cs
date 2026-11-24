using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Voice;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Api.Voice;

/// <summary>
/// A staff voice note written down with Azure Speech fast transcription, as the API's own managed identity: the
/// same speech resource that carries live voice, so a workspace that can talk to IIP can also record a voice note
/// without connecting a separate provider. The recording is read and dropped, never stored.
/// </summary>
public sealed class AzureSpeechStaffTranscriber(VoiceLiveOptions options, TokenCredential credential, IHttpClientFactory httpClientFactory) : IStaffVoiceTranscriber
{
    private const string Scope = "https://cognitiveservices.azure.com/.default";
    private const string ApiVersion = "2024-11-15";
    private const int MaxAudioBytes = 8 * 1024 * 1024;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ResolvedSpeechEndpoint);

    public Task<bool> IsStaffTranscriptionAvailableAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult(IsConfigured);

    public async Task<string?> TranscribeAudioAsync(
        TenantScope scope, byte[] audio, string? contentType, SupportedLanguageCode? preferredLanguage, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || audio.Length == 0 || audio.Length > MaxAudioBytes)
        {
            return null;
        }

        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([Scope]), cancellationToken);
            using var form = new MultipartFormDataContent();
            var content = new ByteArrayContent(audio);
            content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "audio/wav" : contentType.Split(';')[0].Trim());
            form.Add(content, "audio", "voice-note.wav");

            // English first; Swahili is offered so a note in either is written in its own language.
            form.Add(new StringContent("{\"locales\":[\"en-US\",\"sw-KE\"]}", System.Text.Encoding.UTF8, "application/json"), "definition");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ResolvedSpeechEndpoint}/speechtotext/transcriptions:transcribe?api-version={ApiVersion}") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await httpClientFactory.CreateClient(nameof(AzureSpeechStaffTranscriber)).SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var heard = body.RootElement.TryGetProperty("combinedPhrases", out var phrases) && phrases.ValueKind == JsonValueKind.Array
                ? string.Join(' ', phrases.EnumerateArray().Select(phrase => phrase.TryGetProperty("text", out var text) ? text.GetString() : null).Where(text => !string.IsNullOrWhiteSpace(text)))
                : string.Empty;
            return string.IsNullOrWhiteSpace(heard) ? null : heard.Trim();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or AuthenticationFailedException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}

/// <summary>
/// Uses the workspace's own speech provider (Sunbird, which also reads Luganda) when one is connected, and the
/// platform's Azure speech otherwise, so the record button is never a dead control while live voice works.
/// </summary>
public sealed class StaffVoiceTranscriberWithFallback(IStaffVoiceTranscriber primary, AzureSpeechStaffTranscriber fallback) : IStaffVoiceTranscriber
{
    public async Task<bool> IsStaffTranscriptionAvailableAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await primary.IsStaffTranscriptionAvailableAsync(scope, cancellationToken)
        || await fallback.IsStaffTranscriptionAvailableAsync(scope, cancellationToken);

    public async Task<string?> TranscribeAudioAsync(
        TenantScope scope, byte[] audio, string? contentType, SupportedLanguageCode? preferredLanguage, CancellationToken cancellationToken = default)
    {
        if (await primary.IsStaffTranscriptionAvailableAsync(scope, cancellationToken))
        {
            var text = await primary.TranscribeAudioAsync(scope, audio, contentType, preferredLanguage, cancellationToken);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return await fallback.TranscribeAudioAsync(scope, audio, contentType, preferredLanguage, cancellationToken);
    }
}
