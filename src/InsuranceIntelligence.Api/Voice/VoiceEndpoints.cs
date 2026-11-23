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
