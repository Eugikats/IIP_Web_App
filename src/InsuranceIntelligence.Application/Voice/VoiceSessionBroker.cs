using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Voice;

/// <summary>Live voice cannot be started, and why, in words a person can read.</summary>
public sealed class VoiceUnavailableException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;
}

public sealed record VoiceStarted(string SessionId, string SdpAnswer, int MaxMinutes);

public interface IVoiceSessionBroker
{
    bool IsConfigured { get; }

    /// <summary>Warms the link to Voice Live while the person's phone gets its microphone ready, and takes a place for the call.</summary>
    Task<string> PrepareAsync(TenantContext context, CancellationToken cancellationToken);

    Task<VoiceStarted> StartAsync(TenantContext context, string? prepareId, string sdpOffer, Guid? conversationId, int? timeZoneOffsetMinutes, CancellationToken cancellationToken);

    /// <summary>The person's own call, or null: another person's call is never returned.</summary>
    VoiceSession? Find(string sessionId, TenantContext context);

    Task SetLockedAsync(VoiceSession session, bool locked, CancellationToken cancellationToken);

    Task EndAsync(VoiceSession session, string reason);
}

/// <summary>
/// Places and runs live calls. The person's phone talks to Voice Live directly for audio; this holds the
/// control link so the model can use tools as the signed-in person, and so the call can be limited,
/// locked, audited and ended. No audio passes through here and none is stored.
/// </summary>
public sealed class VoiceSessionBroker(
    VoiceLiveOptions options,
    IVoiceLiveTransportFactory transports,
    IVoiceLimits limits,
    VoiceToolbox toolbox,
    IVoiceScopeFactory scopes,
    TimeProvider? clock = null) : IVoiceSessionBroker
{
    private static readonly TimeSpan PreparedLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<string, Call> calls = new();
    private readonly ConcurrentDictionary<string, Prepared> prepared = new();
    private TimeProvider Clock => clock ?? TimeProvider.System;

    public bool IsConfigured => options.IsConfigured;

    public async Task<string> PrepareAsync(TenantContext context, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var lease = limits.TryOpenSession(context.TenantId, context.UserId, out var decision);
        if (lease is null)
        {
            throw new VoiceUnavailableException(decision.Code, decision.Message, 429);
        }

        var transport = transports.Create();
        try
        {
            await transport.ConnectAsync(cancellationToken);
        }
        catch (Exception) when (cancellationToken is { IsCancellationRequested: false })
        {
            lease.Dispose();
            await transport.DisposeAsync();
            throw new VoiceUnavailableException("unreachable", "Live voice could not be reached just now. Try again, or type.", 502);
        }

        var id = NewId();
        var entry = new Prepared(context, transport, lease);
        prepared[id] = entry;
        _ = Task.Delay(PreparedLifetime).ContinueWith(async _ =>
        {
            if (prepared.TryRemove(id, out var stale))
            {
                stale.Lease.Dispose();
                await stale.Transport.DisposeAsync();
            }
        }, TaskScheduler.Default);
        return id;
    }

    public async Task<VoiceStarted> StartAsync(TenantContext context, string? prepareId, string sdpOffer, Guid? conversationId, int? timeZoneOffsetMinutes, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(sdpOffer) || sdpOffer.Length > 64_000)
        {
            throw new VoiceUnavailableException("invalid_offer", "The call could not be set up. Try again.", 400);
        }

        IVoiceLiveTransport transport;
        VoiceSessionLease lease;
        if (prepareId is not null && prepared.TryRemove(prepareId, out var warm) && warm.Context.IsSamePerson(context))
        {
            transport = warm.Transport;
            lease = warm.Lease;
        }
        else
        {
            var fresh = limits.TryOpenSession(context.TenantId, context.UserId, out var decision)
                ?? throw new VoiceUnavailableException(decision.Code, decision.Message, 429);
            lease = fresh;
            transport = transports.Create();
            try
            {
                await transport.ConnectAsync(cancellationToken);
            }
            catch (Exception) when (cancellationToken is { IsCancellationRequested: false })
            {
                lease.Dispose();
                await transport.DisposeAsync();
                throw new VoiceUnavailableException("unreachable", "Live voice could not be reached just now. Try again, or type.", 502);
            }
        }

        var session = new VoiceSession(NewId(), context, conversationId, timeZoneOffsetMinutes, lease, Clock);
        try
        {
            var instructions = await BuildInstructionsAsync(context, cancellationToken);
            await transport.SendAsync(VoiceLiveProtocol.CreateCall(sdpOffer, VoiceLiveProtocol.SessionSettings(options, instructions, VoiceToolbox.Definitions())), cancellationToken);
            var answer = await ReadAnswerAsync(transport, cancellationToken);

            var call = new Call(session, transport);
            calls[session.Id] = call;
            await AuditAsync(session, "voice.session.started", "A live voice conversation started.");
            call.Pump = Task.Run(() => PumpAsync(call), CancellationToken.None);
            _ = Task.Run(() => WatchdogAsync(call), CancellationToken.None);
            if (conversationId is null)
            {
                // A new conversation: IIP speaks first. Carrying on an earlier chat, or reconnecting, does not greet again.
                _ = Task.Run(() => OpenAsync(call), CancellationToken.None);
            }

            session.Cancellation.CancelAfter(limits.MaxSessionLength);
            return new VoiceStarted(session.Id, answer, (int)limits.MaxSessionLength.TotalMinutes);
        }
        catch
        {
            lease.Dispose();
            await transport.DisposeAsync();
            throw;
        }
    }

    public VoiceSession? Find(string sessionId, TenantContext context) =>
        calls.TryGetValue(sessionId, out var call) && call.Session.IsOwnedBy(context) ? call.Session : null;

    public async Task SetLockedAsync(VoiceSession session, bool locked, CancellationToken cancellationToken)
    {
        if (session.Locked == locked)
        {
            return;
        }

        session.Locked = locked;
        session.LastActivityAt = Clock.GetUtcNow();
        if (!calls.TryGetValue(session.Id, out var call))
        {
            return;
        }

        try
        {
            if (locked)
            {
                // Stop mid-sentence, then tell the model why, so nothing more is read out.
                await call.Transport.SendAsync("{\"type\":\"response.cancel\"}", cancellationToken);
            }

            await call.Transport.SendAsync(
                VoiceLiveProtocol.SystemNote(locked
                    ? "The phone is now locked. Do not read out any claim, customer or business figure until it is unlocked. You may say the phone is locked."
                    : "The phone is unlocked again. You may answer questions as before."),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The link is gone; the pump ends the call.
        }

        await AuditAsync(session, locked ? "voice.session.locked" : "voice.session.unlocked", locked ? "The phone was locked during a live conversation." : "The phone was unlocked during a live conversation.");
    }

    public async Task EndAsync(VoiceSession session, string reason)
    {
        if (!calls.TryRemove(session.Id, out var call))
        {
            return;
        }

        session.Cancellation.Cancel();
        session.Events.Writer.TryComplete();
        call.Session.Lease.Dispose();
        var seconds = (Clock.GetUtcNow() - session.StartedAt).TotalSeconds;
        limits.AddCallSeconds(session.Context.TenantId, session.Context.UserId, seconds);
        try
        {
            await call.Transport.DisposeAsync();
        }
        catch (Exception)
        {
            // Already closed.
        }

        await AuditAsync(session, "voice.session.ended", $"The live conversation ended ({reason}) after {(int)seconds} seconds with {session.ToolCalls} tool call(s).");
    }

    /// <summary>How long after the answer is sent the audio link is up, so the first words are not lost.</summary>
    private static readonly TimeSpan OpenerDelay = TimeSpan.FromMilliseconds(1800);

    /// <summary>Has IIP speak first, so the person hears something useful instead of waiting for a question.</summary>
    private async Task OpenAsync(Call call)
    {
        var session = call.Session;
        var token = session.Cancellation.Token;
        try
        {
            await Task.Delay(OpenerDelay, Clock, token);
            if (session.Locked || token.IsCancellationRequested)
            {
                return;
            }

            await call.Transport.SendAsync(
                VoiceLiveProtocol.SystemNote("The call has just connected and the person has not spoken yet. Begin as your instructions say: greet them in one short sentence, say a brief 'let me check' phrase, look up the overview, then offer where to start."),
                token);
            await call.Transport.SendAsync(VoiceLiveProtocol.ResponseCreate(), token);
        }
        catch (Exception)
        {
            // The call ended or the link dropped before the opener could be sent; the pump closes the call.
        }
    }

    private async Task PumpAsync(Call call)
    {
        var session = call.Session;
        var token = session.Cancellation.Token;
        var reason = "closed";
        try
        {
            while (!token.IsCancellationRequested)
            {
                var raw = await call.Transport.ReceiveAsync(token);
                if (raw is null)
                {
                    reason = "dropped";
                    break;
                }

                var message = VoiceLiveProtocol.Parse(raw);
                if (!message.IsFunctionCall)
                {
                    continue;
                }

                session.LastActivityAt = Clock.GetUtcNow();
                var result = await toolbox.ExecuteAsync(session, message.Name!, message.Arguments, token);
                await AuditAsync(session, "voice.tool.called", $"{message.Name}: {result.Outcome}");
                await call.Transport.SendAsync(VoiceLiveProtocol.FunctionResult(message.CallId!, result.OutputJson), token);
                await call.Transport.SendAsync(VoiceLiveProtocol.ResponseCreate(), token);
                if (result.EndsCall)
                {
                    // Let the goodbye be spoken, then close.
                    reason = "ended by the person";
                    session.Cancellation.CancelAfter(TimeSpan.FromSeconds(6));
                }
            }
        }
        catch (OperationCanceledException)
        {
            reason = session.EndRequested ? "ended by the person" : "time limit";
        }
        catch (Exception)
        {
            reason = "link failed";
        }

        await EndAsync(session, reason);
    }

    private async Task WatchdogAsync(Call call)
    {
        var session = call.Session;
        try
        {
            while (!session.Cancellation.IsCancellationRequested)
            {
                await Task.Delay(WatchdogInterval, Clock, session.Cancellation.Token);
                if (Clock.GetUtcNow() - session.LastActivityAt > TimeSpan.FromSeconds(options.IdleSeconds))
                {
                    await EndAsync(session, "idle");
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The call ended.
        }
    }

    private async Task<string> ReadAnswerAsync(IVoiceLiveTransport transport, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AnswerTimeout);
        try
        {
            while (true)
            {
                var raw = await transport.ReceiveAsync(timeout.Token)
                    ?? throw new VoiceUnavailableException("unreachable", "Live voice closed before the call was ready. Try again, or type.", 502);
                var message = VoiceLiveProtocol.Parse(raw);
                if (message.Type == "rtc.call.sdp.created" && !string.IsNullOrWhiteSpace(message.SdpAnswer))
                {
                    return message.SdpAnswer;
                }

                if (message.Type == "error")
                {
                    throw new VoiceUnavailableException("rejected", "Live voice could not start this call. Try again, or type.", 502);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VoiceUnavailableException("timeout", "Live voice took too long to answer. Try again, or type.", 504);
        }
    }

    private async Task<string> BuildInstructionsAsync(TenantContext context, CancellationToken cancellationToken)
    {
        AgentBehaviourProfile? profile = null;
        try
        {
            using var scope = scopes.Create(context);
            profile = await scope.Profiles.GetCurrentAsync(context.Scope, "command-center", cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Without a saved profile the agent runs on its built-in voice instructions.
        }

        return AgentBehaviourComposer.Compose(profile, VoiceInstructions);
    }

    internal const string VoiceInstructions =
        "You are IIP, a voice assistant for insurance staff, speaking aloud on a phone call. Speak in short, plain sentences, one or two at a time, in the language the person uses. " +
        "You only help with this workspace: its claims, customer acquisition (prospects and new enquiries), customers, approvals, documents, and questions about the insurer's own products. " +
        "You are not a general assistant. If asked anything else, such as general knowledge, politics, news, people, sport or opinions, do not answer it, even if you know the answer. " +
        "Say kindly that it is outside what you help with here, then offer two or three things you can do instead. " +
        "When the call begins, greet the person warmly in one short sentence, then call ask_analyst twice for a quick overview: first 'what needs my attention today', then 'how many new prospects came in over the past 3 days'. " +
        "Say the two or three most useful things you found, and ask which they would like to start with, for example claims, acquisition or approvals. Never open by asking what they want to talk about with nothing to offer. " +
        "Before you call any tool, say a brief natural phrase first, such as 'Sure, let me pull that up', 'One moment while I check', or 'Right, looking at that now', varied each time and never more than a few words, so the person is never left in silence. " +
        "When you call ask_analyst, pass the person's own question in their own words as closely as you can. " +
        "For anything about the workspace's claims, customers, prospects, approvals, documents or figures, call ask_analyst and say only what it returns; never give a figure from memory. " +
        "Never say 'approved sources' or talk about where your information is stored; just answer. If ask_analyst has nothing on the question, say so briefly and offer what you can look at. " +
        "Use make_visual when a chart or table would help, and say one sentence about it. Use show_on_screen only to offer to open something already mentioned. " +
        "Use prepare_draft to save a customer update for a person to approve; say plainly that nothing was sent. You can read, show and prepare, and nothing else: " +
        "never say you sent, approved, paid or changed anything. If a tool says the phone is locked, say so and give no figures. " +
        "Do not use emoji, dashes as punctuation, or lists; this is spoken.";

    private async Task AuditAsync(VoiceSession session, string action, string reason)
    {
        try
        {
            using var scope = scopes.Create(session.Context);
            await scope.Audit.WriteAsync(new AuditEvent(
                Guid.NewGuid(), session.Context.Scope, null, null, session.Context.UserId, ActorType.User, action,
                Clock.GetUtcNow(), null, null, [], "voice-live", null, "accepted", null, reason, session.Id));
        }
        catch (Exception)
        {
            // A missing audit row must not cut a person off mid-call; the call itself is bounded and limited.
        }
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new VoiceUnavailableException("not_configured", "Live voice is not set up for this environment. You can still record a voice note, or type.", 503);
        }
    }

    private static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private sealed record Prepared(TenantContext Context, IVoiceLiveTransport Transport, VoiceSessionLease Lease);

    private sealed class Call(VoiceSession session, IVoiceLiveTransport transport)
    {
        public VoiceSession Session { get; } = session;

        public IVoiceLiveTransport Transport { get; } = transport;

        public Task? Pump { get; set; }
    }
}

internal static class TenantContextExtensions
{
    public static bool IsSamePerson(this TenantContext a, TenantContext b) =>
        a.TenantId == b.TenantId && a.WorkspaceId == b.WorkspaceId && a.UserId == b.UserId;
}
