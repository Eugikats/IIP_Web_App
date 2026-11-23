using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Voice;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.CommandCenter;

/// <summary>
/// A staff voice note: <c>POST /api/v1/command-center/voice-note</c>. The recording is turned into words
/// and returned to the person to read and send. It is not sent to the agent here, so a mis-heard word is
/// seen and fixed before it becomes a question, and nothing is stored: the audio is read, transcribed and dropped.
/// </summary>
public sealed record VoiceReplyRequest(string? Text, string? Language);

public static class CommandCenterVoiceEndpoints
{
    // A minute of speech is well under this; a larger file is not a voice note.
    private const long MaxBytes = 8 * 1024 * 1024;

    public static async Task<IResult> Transcribe(
        IStaffVoiceTranscriber transcriber,
        IVoiceLimits limits,
        ITenantContextSetter tenantContextSetter,
        ITenantContextAccessor tenantContextAccessor,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var tenantContextError))
        {
            return tenantContextError;
        }

        if (!httpContext.Request.HasFormContentType)
        {
            return Fail(StatusCodes.Status400BadRequest, "invalid_request", "Send the recording as multipart form data.");
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("audio") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Fail(StatusCodes.Status400BadRequest, "no_audio", "There was no recording to read.");
        }

        if (file.Length > MaxBytes)
        {
            return Fail(StatusCodes.Status413PayloadTooLarge, "too_long", "That recording is too long. Keep a voice note under a minute.");
        }

        var scope = tenantContextAccessor.Current.Scope;
        if (!await transcriber.IsStaffTranscriptionAvailableAsync(scope, cancellationToken))
        {
            return Fail(StatusCodes.Status503ServiceUnavailable, "not_configured", "Voice notes are not set up for this workspace. An administrator can connect a speech provider in Settings.");
        }

        var decision = limits.TryUseTranscription(scope.TenantId, tenantContextAccessor.Current.UserId, VoiceLimits.EstimateSeconds(file.Length, file.ContentType));
        if (!decision.Allowed)
        {
            return Fail(StatusCodes.Status429TooManyRequests, decision.Code, decision.Message);
        }

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var language = ParseLanguage(form["language"].ToString());
        var text = await transcriber.TranscribeAudioAsync(scope, buffer.ToArray(), file.ContentType, language, cancellationToken);
        return string.IsNullOrWhiteSpace(text)
            ? Fail(StatusCodes.Status422UnprocessableEntity, "not_understood", "I could not make out any words in that recording. Try again, or type it.")
            : Results.Ok(new { text = DisplayText.WithoutEmDashes(text) });
    }

    /// <summary>Whether this workspace can turn speech into words, so the portal can offer voice or say it is not set up.</summary>
    public static async Task<IResult> Status(
        IStaffVoiceTranscriber transcriber,
        ITenantContextSetter tenantContextSetter,
        ITenantContextAccessor tenantContextAccessor,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var tenantContextError))
        {
            return tenantContextError;
        }

        var available = await transcriber.IsStaffTranscriptionAvailableAsync(tenantContextAccessor.Current.Scope, cancellationToken);
        return Results.Ok(new { available });
    }

    /// <summary>
    /// A written answer, said aloud in Luganda or Swahili: <c>POST /api/v1/command-center/voice-reply</c>. The English answer
    /// is translated first, so the voice never reads English words in a Luganda voice; when it cannot be translated the person
    /// is told and gets the English text, no audio. English replies are spoken by the realtime call or the device, not here.
    /// </summary>
    public static async Task<IResult> Speak(
        VoiceReplyRequest request,
        IVoiceNoteSpeaker speaker,
        ITranslationService translation,
        IVoiceLimits limits,
        ITenantContextSetter tenantContextSetter,
        ITenantContextAccessor tenantContextAccessor,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var tenantContextError))
        {
            return tenantContextError;
        }

        var text = request.Text?.Trim() ?? string.Empty;
        var language = ParseLanguage(request.Language);
        if (text.Length == 0 || text.Length > 600)
        {
            return Fail(StatusCodes.Status400BadRequest, "invalid_text", "Send between 1 and 600 characters to be spoken.");
        }

        if (language is not (SupportedLanguageCode.Lg or SupportedLanguageCode.Sw))
        {
            return Fail(StatusCodes.Status400BadRequest, "invalid_language", "Spoken replies here are for Luganda and Swahili.");
        }

        var scope = tenantContextAccessor.Current.Scope;
        var decision = limits.TryUseTranscription(scope.TenantId, tenantContextAccessor.Current.UserId, text.Length / 15.0);
        if (!decision.Allowed)
        {
            return Fail(StatusCodes.Status429TooManyRequests, decision.Code, decision.Message);
        }

        var translated = await translation.TranslateAsync(text, SupportedLanguageCode.En, language.Value, scope, cancellationToken);
        if (!translated.Translated)
        {
            return Results.Ok(new { text = DisplayText.WithoutEmDashes(text), language = "en", translated = false, audio = (string?)null, mimeType = (string?)null });
        }

        var spoken = DisplayText.WithoutEmDashes(translated.Text);
        var audio = await speaker.SpeakAsync(scope, spoken, language.Value, cancellationToken);
        return Results.Ok(new
        {
            text = spoken,
            language = request.Language!.Trim().ToLowerInvariant(),
            translated = true,
            audio = audio is null ? null : Convert.ToBase64String(audio),
            mimeType = audio is null ? null : "audio/ogg",
        });
    }

    public static SupportedLanguageCode? ParseLanguage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "sw" => SupportedLanguageCode.Sw,
        "lg" => SupportedLanguageCode.Lg,
        "en" => SupportedLanguageCode.En,
        _ => null,
    };

    private static IResult Fail(int status, string code, string message) =>
        Results.Json(new { code, message }, statusCode: status);
}
