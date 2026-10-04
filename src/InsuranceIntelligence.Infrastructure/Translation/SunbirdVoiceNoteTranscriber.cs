using System.Net.Http.Headers;
using System.Text.Json;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Translation;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Translation;

/// <summary>
/// Voice notes through Sunbird AI's speech-to-text (POST /tasks/audio/transcriptions), using the
/// same per-tenant Sunbird connection the dashboard already holds for translation, so there is one
/// key to manage and none in startup configuration. WhatsApp voice notes are OGG/Opus, which Sunbird
/// accepts as they are, so nothing is transcoded.
///
/// Sunbird needs a language named up front and says its auto-detection is unreliable. Its Luganda
/// model also writes English speech down as English (checked live), so Luganda is the default for
/// Uganda and Swahili is used only for a customer who chose Swahili.
///
/// The audio is sent to Sunbird for transcription, the same processor that already translates this
/// tenant's text messages; it is not stored by IIP.
/// </summary>
public sealed class SunbirdVoiceNoteTranscriber(
    IIntegrationConnectionRepository connectionRepository,
    IConnectionSecretProtector protector,
    IWhatsAppInboundProvider whatsAppProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<SunbirdVoiceNoteTranscriber> logger) : IVoiceNoteTranscriber, IVoiceNoteSpeaker
{
    private const string SunbirdProviderKey = "Sunbird";

    // Sunbird's first request after a quiet spell was measured at almost 40 seconds; later ones at
    // about 3. The assistant runs after Meta has been answered, so waiting is safe.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    // Meta caps voice notes at 16 MB; anything larger is not a voice note we can use.
    private const int MaxAudioBytes = 16 * 1024 * 1024;

    public async Task<bool> IsAvailableAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await SettingsAsync(scope, cancellationToken) is not null && whatsAppProvider.IsConfigured;

    public async Task<string?> TranscribeAsync(
        TenantScope scope, string mediaId, SupportedLanguageCode? preferredLanguage, string? businessPhoneNumberId = null, CancellationToken cancellationToken = default)
    {
        var settings = await SettingsAsync(scope, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(mediaId))
        {
            return null;
        }

        try
        {
            var media = string.IsNullOrWhiteSpace(businessPhoneNumberId)
                ? await whatsAppProvider.DownloadMediaAsync(mediaId, cancellationToken)
                : await whatsAppProvider.DownloadMediaAsync(mediaId, businessPhoneNumberId, cancellationToken);
            if (media.ContentBytes.Length == 0 || media.ContentBytes.Length > MaxAudioBytes)
            {
                return null;
            }

            using var form = new MultipartFormDataContent();
            var audio = new ByteArrayContent(media.ContentBytes);
            audio.Headers.ContentType = MediaTypeHeaderValue.Parse(BaseMediaType(media.ContentType));
            form.Add(audio, "audio", "voice-note.ogg");
            form.Add(new StringContent(LanguageFor(preferredLanguage)), "language");

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Endpoint}/tasks/audio/transcriptions") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            using var response = await httpClientFactory.CreateClient(nameof(SunbirdVoiceNoteTranscriber)).SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Sunbird transcription failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var text = ReadTranscript(json.RootElement);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Voice note transcription failed safely. ErrorType={ErrorType}", exception.GetType().Name);
            return null;
        }
    }

    /// <summary>Sunbird's text-to-speech (POST /tasks/audio/speech, streamed WAV), encoded to the
    /// OGG/Opus WhatsApp plays as a voice note. Measured at about 12 seconds for a short reply.</summary>
    public async Task<byte[]?> SpeakAsync(TenantScope scope, string text, SupportedLanguageCode language, CancellationToken cancellationToken = default)
    {
        var settings = await SettingsAsync(scope, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Endpoint}/tasks/audio/speech")
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    text = text.Trim(),
                    language = SpeechLanguageFor(language),
                    response_mode = "stream",
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            using var response = await httpClientFactory.CreateClient(nameof(SunbirdVoiceNoteTranscriber)).SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Sunbird speech failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var wav = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            var ogg = OggOpusVoiceNote.FromWav(wav);
            if (ogg is null)
            {
                logger.LogWarning("Sunbird speech returned audio that could not be encoded as a voice note ({Bytes} bytes).", wav.Length);
            }

            return ogg;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Voice reply failed safely. ErrorType={ErrorType}", exception.GetType().Name);
            return null;
        }
    }

    internal static string SpeechLanguageFor(SupportedLanguageCode language) => language switch
    {
        SupportedLanguageCode.Lg => "lug",
        SupportedLanguageCode.Sw => "swa",
        _ => "eng",
    };

    internal static string? ReadTranscript(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("audio_transcription", out var text)
        && text.ValueKind == JsonValueKind.String
            ? text.GetString()
            : null;

    internal static string LanguageFor(SupportedLanguageCode? preferred) =>
        preferred == SupportedLanguageCode.Sw ? "swa" : "lug";

    private static string BaseMediaType(string? contentType)
    {
        // Meta reports "audio/ogg; codecs=opus"; the parameter is dropped, the type kept.
        var baseType = contentType?.Split(';')[0].Trim();
        return string.IsNullOrWhiteSpace(baseType) || !baseType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            ? "audio/ogg"
            : baseType;
    }

    private async Task<SunbirdSettings?> SettingsAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var connection = await connectionRepository.GetAsync(scope, ConnectionType.Translation, cancellationToken);
        if (connection is not { IsActive: true }
            || !string.Equals(connection.ProviderKey, SunbirdProviderKey, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        return SunbirdSettings.TryParse(settings, out var parsed, out _) ? parsed : null;
    }
}
