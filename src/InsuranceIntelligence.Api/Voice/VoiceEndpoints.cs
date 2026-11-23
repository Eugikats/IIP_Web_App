using System.Text.Json;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Voice;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Api.Voice;

public sealed record StartVoiceSessionRequest(string? PrepareId, string? SdpOffer, Guid? ConversationId, int? TimeZoneOffsetMinutes);

public sealed record DeviceCheckRequest(string? Token);

public sealed record VoiceMetricsRequest(Dictionary<string, double>? Marks);

/// <summary>
/// Live voice: <c>/api/v1/voice/*</c>. The phone's audio goes straight to Voice Live; these endpoints set the call up,
/// carry what the agent wants shown, lock and end it. A call belongs to the person who opened it: anyone else
/// asking for it is told it does not exist.
/// </summary>
public static class VoiceEndpoints
{
    private static readonly TimeSpan Ping = TimeSpan.FromSeconds(15);

    public static async Task<IResult> Status(
        IVoiceSessionBroker broker, VoiceLiveOptions options, IStaffVoiceTranscriber transcriber,
        ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http, CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        var turnBased = await transcriber.IsStaffTranscriptionAvailableAsync(accessor.Current.Scope, cancellationToken);
        return Results.Ok(new { available = broker.IsConfigured, turnBasedAvailable = turnBased, processedIn = options.ProcessingRegion });
    }

    public static async Task<IResult> Prepare(
        IVoiceSessionBroker broker, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http, CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        try
        {
            return Results.Ok(new { prepareId = await broker.PrepareAsync(accessor.Current, cancellationToken) });
        }
        catch (VoiceUnavailableException exception)
        {
            return Fail(exception);
        }
    }

    public static async Task<IResult> Start(
        StartVoiceSessionRequest request, IVoiceSessionBroker broker, ITenantContextSetter setter, ITenantContextAccessor accessor,
        InfrastructureOptions infrastructure, HttpContext http, CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        try
        {
            var started = await broker.StartAsync(accessor.Current, request.PrepareId, request.SdpOffer ?? string.Empty, request.ConversationId, request.TimeZoneOffsetMinutes, cancellationToken);
            return Results.Ok(new { sessionId = started.SessionId, sdpAnswer = started.SdpAnswer, maxMinutes = started.MaxMinutes });
        }
        catch (VoiceUnavailableException exception)
        {
            return Fail(exception);
        }
    }

    public static async Task<IResult> Lock(
        string sessionId, IVoiceSessionBroker broker, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http, CancellationToken cancellationToken) =>
        await Change(sessionId, broker, setter, accessor, infrastructure, http, (session) => broker.SetLockedAsync(session, true, cancellationToken));

    public static async Task<IResult> Unlock(
        string sessionId, IVoiceSessionBroker broker, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http, CancellationToken cancellationToken) =>
        await Change(sessionId, broker, setter, accessor, infrastructure, http, (session) => broker.SetLockedAsync(session, false, cancellationToken));

    public static async Task<IResult> End(
        string sessionId, IVoiceSessionBroker broker, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http) =>
        await Change(sessionId, broker, setter, accessor, infrastructure, http, (session) => broker.EndAsync(session, "ended by the person"));

    /// <summary>Server-sent events for what the agent wants shown: <c>blocks</c>, <c>open</c> and a final <c>ended</c>.</summary>
    public static async Task Events(
        string sessionId, IVoiceSessionBroker broker, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure,
        IOptions<JsonOptions> jsonOptions, HttpContext http, CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            await error.ExecuteAsync(http);
            return;
        }

        var session = broker.Find(sessionId, accessor.Current);
        if (session is null)
        {
            await Results.NotFound().ExecuteAsync(http);
            return;
        }

        var json = jsonOptions.Value.SerializerOptions;
        var response = http.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-transform";
        response.Headers["X-Accel-Buffering"] = "no";

        async Task Send(string name, object data)
        {
            await response.WriteAsync("event: " + name + "\ndata: " + JsonSerializer.Serialize(data, data.GetType(), json) + "\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }

        try
        {
            // The page staying connected shows the person is still there, so a call whose page was closed ends by itself.
            while (!cancellationToken.IsCancellationRequested)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                wait.CancelAfter(Ping);
                try
                {
                    if (!await session.Events.Reader.WaitToReadAsync(wait.Token))
                    {
                        await Send("ended", new { });
                        return;
                    }

                    while (session.Events.Reader.TryRead(out var screenEvent))
                    {
                        await Send(screenEvent.Kind, screenEvent.Data);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await response.WriteAsync(": ping\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }

                session.LastActivityAt = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
    }

    public static IResult Metrics(
        VoiceMetricsRequest request, IVoiceTimings timings, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        timings.Record(accessor.Current.TenantId, request.Marks ?? []);
        return Results.NoContent();
    }

    /// <summary>
    /// The next spoken nudge, or 204 when there is nothing to say right now (outside working hours, the day's allowance
    /// used, or everything offered in the last few days). Asking claims the nudge: it will not be offered again.
    /// With <c>locked=true</c> the wording has no names and no figures.
    /// </summary>
    public static async Task<IResult> NextNudge(
        bool? locked, IVoiceNudges nudges, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http, CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        var context = accessor.Current;
        var nudge = await nudges.NextAsync(context.TenantId + "|" + context.UserId, locked ?? false, cancellationToken);
        return nudge is null ? Results.NoContent() : Results.Ok(nudge);
    }

    // ---- the phone while the app is closed ---------------------------------------------------------------------------

    private const string DeviceHeader = "X-Device-Check";

    /// <summary>
    /// Gives the signed-in app a narrow credential for its background checks (see <see cref="IDeviceChecks"/>). The page passes it
    /// to the Android app, which keeps it in its own private storage. It can ask for the next nudge and confirm the wake
    /// phrase, and nothing else.
    /// </summary>
    public static IResult RegisterDevice(
        IDeviceChecks checks, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        var grant = checks.Issue(accessor.Current);
        return Results.Ok(new { token = grant.Token, expiresAt = grant.ExpiresAt });
    }

    /// <summary>Called on sign-out so a phone that stays signed out does not keep asking.</summary>
    public static IResult RevokeDevice(
        DeviceCheckRequest request, IDeviceChecks checks, ITenantContextSetter setter, InfrastructureOptions infrastructure, HttpContext http)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        checks.Revoke(request.Token);
        return Results.NoContent();
    }

    /// <summary>The next nudge for the phone's background check. Answers 204 when there is nothing to say, 401 when the credential is not known.</summary>
    public static async Task<IResult> DeviceNudge(
        bool? locked, IDeviceChecks checks, IVoiceNudges nudges, ITenantContextSetter setter, HttpContext http, CancellationToken cancellationToken)
    {
        var token = http.Request.Headers[DeviceHeader].ToString();
        var person = checks.Resolve(token);
        if (person is null)
        {
            return Results.Unauthorized();
        }

        if (!checks.TryUse(token, "nudge", 6))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        setter.SetCurrent(person);
        var nudge = await nudges.NextAsync(person.TenantId + "|" + person.UserId, locked ?? false, cancellationToken);
        return nudge is null ? Results.NoContent() : Results.Ok(nudge);
    }

    /// <summary>
    /// The second look at the wake phrase: <c>POST /api/v1/voice/device/wake</c> with the two or three seconds the phone heard
    /// as <c>audio</c> (16 kHz WAV). Twenty a minute per phone. Nothing is kept.
    /// </summary>
    public static async Task<IResult> DeviceWake(
        IDeviceChecks checks, IWakeChecker checker, HttpContext http, CancellationToken cancellationToken)
    {
        var token = http.Request.Headers[DeviceHeader].ToString();
        if (checks.Resolve(token) is null)
        {
            return Results.Unauthorized();
        }

        if (!checks.TryUse(token, "wake", 20))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (!http.Request.HasFormContentType)
        {
            return Results.BadRequest(new { code = "invalid_request", message = "Send the clip as multipart form data." });
        }

        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("audio") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length < 4000 || file.Length > 400_000)
        {
            return Results.BadRequest(new { code = "no_audio", message = "There was no usable clip." });
        }

        if (!checker.IsConfigured)
        {
            return Results.Json(new { code = "not_configured", message = "Voice is not set up for this environment." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var result = await checker.CheckAsync(buffer.ToArray(), cancellationToken);
        return result is null
            ? Results.Json(new { code = "check_failed", message = "The wake check could not be made." }, statusCode: StatusCodes.Status502BadGateway)
            : Results.Ok(new { wake = result.Wake, heard = result.Heard });
    }

    public static IResult ReadMetrics(
        IVoiceTimings timings, ITenantContextSetter setter, ITenantContextAccessor accessor, InfrastructureOptions infrastructure, HttpContext http)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        return Results.Ok(new { timings = timings.Summarise(accessor.Current.TenantId) });
    }

    private static async Task<IResult> Change(
        string sessionId, IVoiceSessionBroker broker, ITenantContextSetter setter, ITenantContextAccessor accessor,
        InfrastructureOptions infrastructure, HttpContext http, Func<VoiceSession, Task> change)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, infrastructure, out var error))
        {
            return error;
        }

        var session = broker.Find(sessionId, accessor.Current);
        if (session is null)
        {
            return Results.NotFound();
        }

        await change(session);
        return Results.NoContent();
    }

    private static IResult Fail(VoiceUnavailableException exception) =>
        Results.Json(new { code = exception.Code, message = exception.Message }, statusCode: exception.StatusCode);
}
