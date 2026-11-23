using System.Text.Json;
using System.Threading.Channels;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Voice;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The voice broker against a scripted Voice Live socket: nothing here touches Azure.</summary>
public sealed class VoiceSessionBrokerTests
{
    private static readonly TenantContext Alice = new("tenant-a", "ws-1", "alice", ["ClaimsManager"]);
    private static readonly TenantContext Bob = new("tenant-a", "ws-1", "bob", ["ClaimsManager"]);
    private static readonly TenantContext OtherTenantAlice = new("tenant-b", "ws-1", "alice", ["ClaimsManager"]);

    // ---- setting up a call --------------------------------------------------------------------

    [Fact]
    public async Task A_call_gets_an_answer_and_the_session_carries_the_rules_the_person_cannot_change()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);

        Assert.Equal("answer-sdp", started.SdpAnswer);
        var create = JsonDocument.Parse(Assert.Single(rig.Transport.Sent, m => m.Contains("rtc.call.sdp.create"))).RootElement;
        Assert.Equal("offer-sdp", create.GetProperty("sdp_offer").GetString());

        var session = create.GetProperty("session");
        var instructions = session.GetProperty("instructions").GetString()!;
        Assert.StartsWith(AgentBehaviourComposer.PlatformPreamble, instructions);
        Assert.Equal("azure_semantic_vad", session.GetProperty("turn_detection").GetProperty("type").GetString());
        Assert.Equal(500, session.GetProperty("turn_detection").GetProperty("silence_duration_ms").GetInt32());
        Assert.True(session.GetProperty("turn_detection").GetProperty("interrupt_response").GetBoolean());

        // The agent can read, show and prepare. There is no tool that sends, approves, pays or changes a status.
        var tools = session.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToArray();
        Assert.Equal(["ask_analyst", "make_visual", "show_on_screen", "prepare_draft", "end_conversation"], tools);
        Assert.DoesNotContain(tools, name => name!.Contains("send") || name.Contains("approve") || name.Contains("pay"));
    }

    [Fact]
    public async Task Live_voice_that_is_not_set_up_says_so_and_connects_to_nothing()
    {
        var rig = new Rig(configured: false);

        var failure = await Assert.ThrowsAsync<VoiceUnavailableException>(() => rig.Broker.PrepareAsync(Alice, CancellationToken.None));

        Assert.Equal("not_configured", failure.Code);
        Assert.Equal(503, failure.StatusCode);
        Assert.Equal(0, rig.Factory.Created);
    }

    [Fact]
    public async Task Preparing_warms_the_link_and_starting_uses_it_without_a_second_connection()
    {
        var rig = new Rig();
        var prepareId = await rig.Broker.PrepareAsync(Alice, CancellationToken.None);
        Assert.Equal(1, rig.Factory.Created);

        await rig.Broker.StartAsync(Alice, prepareId, "offer-sdp", null, null, CancellationToken.None);

        Assert.Equal(1, rig.Factory.Created);
    }

    [Fact]
    public async Task Someone_elses_prepared_link_cannot_be_used()
    {
        var rig = new Rig();
        var prepareId = await rig.Broker.PrepareAsync(Alice, CancellationToken.None);

        await rig.Broker.StartAsync(Bob, prepareId, "offer-sdp", null, null, CancellationToken.None);

        Assert.Equal(2, rig.Factory.Created);
    }

    [Fact]
    public async Task A_person_cannot_hold_more_calls_than_allowed_and_ending_one_frees_a_place()
    {
        var rig = new Rig(new VoiceLimitOptions { MaxConcurrentSessions = 1 });
        var first = await rig.Start(Alice);

        var refused = await Assert.ThrowsAsync<VoiceUnavailableException>(() => rig.Broker.PrepareAsync(Alice, CancellationToken.None));
        Assert.Equal("too_many_calls", refused.Code);
        Assert.Equal(429, refused.StatusCode);
        // Another person is not affected.
        await rig.Start(Bob);

        await rig.Broker.EndAsync(rig.Broker.Find(first.SessionId, Alice)!, "test");
        await rig.Broker.PrepareAsync(Alice, CancellationToken.None);
    }

    [Fact]
    public async Task A_call_that_voice_live_never_answers_fails_cleanly_and_frees_its_place()
    {
        var rig = new Rig(answers: false, limits: new VoiceLimitOptions { MaxConcurrentSessions = 1 });
        rig.Transport.Reject = true;

        var failure = await Assert.ThrowsAsync<VoiceUnavailableException>(() => rig.Broker.StartAsync(Alice, null, "offer-sdp", null, null, CancellationToken.None));

        Assert.Equal("rejected", failure.Code);
        Assert.True(rig.Transport.Disposed);
        rig.Transport.Reject = false;
        await rig.Broker.PrepareAsync(Alice, CancellationToken.None); // the place was released
    }

    // ---- tools, as the signed-in person -------------------------------------------------------

    [Fact]
    public async Task A_spoken_question_runs_the_analyst_as_the_person_and_the_answer_goes_back_to_the_model()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        rig.Conversations.Next = Answer("Four claims came in today.");

        rig.Transport.Incoming(FunctionCall("call-1", "ask_analyst", "{\"question\":\"How many claims came in today?\"}"));
        await rig.Transport.WaitForSent("function_call_output");

        var seen = Assert.Single(rig.Conversations.Requests);
        Assert.Equal("How many claims came in today?", seen.Question);
        Assert.Equal(Alice, rig.Conversations.Contexts.Single());

        var result = JsonDocument.Parse(rig.Transport.Sent.Single(m => m.Contains("function_call_output"))).RootElement.GetProperty("item");
        Assert.Equal("call-1", result.GetProperty("call_id").GetString());
        Assert.Contains("Four claims came in today.", result.GetProperty("output").GetString());
        Assert.Contains(rig.Transport.Sent, m => m.Contains("\"response.create\""));
        Assert.NotNull(rig.Broker.Find(started.SessionId, Alice));
    }

    [Fact]
    public async Task Each_spoken_question_continues_the_same_written_conversation()
    {
        var rig = new Rig();
        await rig.Start(Alice);
        var conversation = Guid.NewGuid();
        rig.Conversations.Next = Answer("One.", conversation);

        rig.Transport.Incoming(FunctionCall("c1", "ask_analyst", "{\"question\":\"first\"}"));
        await rig.Transport.WaitForSent("function_call_output");
        rig.Transport.Incoming(FunctionCall("c2", "ask_analyst", "{\"question\":\"second\"}"));
        await rig.Transport.WaitForSent("function_call_output", count: 2);

        Assert.Null(rig.Conversations.Requests[0].ConversationId);
        Assert.Equal(conversation, rig.Conversations.Requests[1].ConversationId);
    }

    [Fact]
    public async Task The_page_is_told_which_written_conversation_the_call_started_so_typing_can_continue_it()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        var session = rig.Broker.Find(started.SessionId, Alice)!;
        var conversation = Guid.NewGuid();
        rig.Conversations.Next = Answer("One.", conversation);

        await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"first\"}", CancellationToken.None);
        await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"second\"}", CancellationToken.None);

        var told = Assert.Single(Drain(session), e => e.Kind == "conversation");
        Assert.Equal(conversation, ((VoiceConversationEvent)told.Data).ConversationId);
    }

    [Fact]
    public async Task While_the_phone_is_locked_no_tool_gives_anything_but_end_conversation_works()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        var session = rig.Broker.Find(started.SessionId, Alice)!;
        await rig.Broker.SetLockedAsync(session, true, CancellationToken.None);
        Assert.Contains(rig.Transport.Sent, m => m.Contains("response.cancel"));
        Assert.Contains(rig.Transport.Sent, m => m.Contains("now locked"));

        foreach (var (name, args) in new[]
        {
            ("ask_analyst", "{\"question\":\"claims today\"}"),
            ("make_visual", "{\"question\":\"claims by day\"}"),
            ("show_on_screen", "{\"label\":\"claim\"}"),
            ("prepare_draft", "{\"claim_reference\":\"CLM-1\"}"),
        })
        {
            var result = await rig.Toolbox.ExecuteAsync(session, name, args, CancellationToken.None);
            Assert.Equal("locked", result.Outcome);
            Assert.True(JsonDocument.Parse(result.OutputJson).RootElement.GetProperty("locked").GetBoolean(), name);
        }

        Assert.Empty(rig.Conversations.Requests);

        var end = await rig.Toolbox.ExecuteAsync(session, "end_conversation", "{}", CancellationToken.None);
        Assert.True(end.EndsCall);

        session.EndRequested = false;
        await rig.Broker.SetLockedAsync(session, false, CancellationToken.None);
        rig.Conversations.Next = Answer("Fine.");
        var after = await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"claims today\"}", CancellationToken.None);
        Assert.Equal("ok", after.Outcome);
    }

    [Fact]
    public async Task A_draft_is_saved_for_approval_and_nothing_is_sent()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        rig.Conversations.Next = new CommandCenterQueryResponse(
            [new TextBlock("Draft customer update prepared."), new DraftCommunicationBlock("draft-9", "Dear customer, your claim is being reviewed.", true)],
            Guid.NewGuid(), "c", "None", "v1", "Internal", false, DateTimeOffset.UtcNow);

        var result = await rig.Toolbox.ExecuteAsync(rig.Broker.Find(started.SessionId, Alice)!, "prepare_draft", "{\"claim_reference\":\"CLM-1234\"}", CancellationToken.None);

        Assert.Equal("ok", result.Outcome);
        var output = JsonDocument.Parse(result.OutputJson).RootElement;
        Assert.True(output.GetProperty("prepared").GetBoolean());
        Assert.Equal("saved for approval", output.GetProperty("status").GetString());
        Assert.Contains("Nothing has been sent", output.GetProperty("message").GetString());
        Assert.Contains("CLM-1234", rig.Conversations.Requests.Single().Question);
    }

    [Theory]
    [InlineData("prepare_draft", "{\"claim_reference\":\"CLM 1; drop table\"}")]
    [InlineData("prepare_draft", "{}")]
    [InlineData("ask_analyst", "{\"question\":\"\"}")]
    [InlineData("ask_analyst", "not json")]
    [InlineData("make_visual", "{\"question\":123}")]
    public async Task Badly_formed_requests_are_refused_without_reaching_the_analyst(string name, string args)
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);

        var result = await rig.Toolbox.ExecuteAsync(rig.Broker.Find(started.SessionId, Alice)!, name, args, CancellationToken.None);

        Assert.Equal("invalid", result.Outcome);
        Assert.Empty(rig.Conversations.Requests);
    }

    [Fact]
    public async Task A_tool_the_model_invents_is_refused()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);

        var result = await rig.Toolbox.ExecuteAsync(rig.Broker.Find(started.SessionId, Alice)!, "send_email", "{\"to\":\"x\"}", CancellationToken.None);

        Assert.Equal("refused", result.Outcome);
        Assert.Empty(rig.Conversations.Requests);
    }

    [Fact]
    public async Task Opening_a_screen_only_offers_what_an_answer_really_returned()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        var session = rig.Broker.Find(started.SessionId, Alice)!;

        var before = await rig.Toolbox.ExecuteAsync(session, "show_on_screen", "{\"label\":\"overdue\"}", CancellationToken.None);
        Assert.False(JsonDocument.Parse(before.OutputJson).RootElement.GetProperty("shown").GetBoolean());

        rig.Conversations.Next = new CommandCenterQueryResponse(
            [new TextBlock("Open."), new LinkBlock("Overdue claims", "/app/claims?sla=breached")],
            Guid.NewGuid(), "c", "None", "v1", "Internal", false, DateTimeOffset.UtcNow);
        await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"what is overdue\"}", CancellationToken.None);
        var after = await rig.Toolbox.ExecuteAsync(session, "show_on_screen", "{\"label\":\"overdue\"}", CancellationToken.None);

        Assert.True(JsonDocument.Parse(after.OutputJson).RootElement.GetProperty("shown").GetBoolean());
        var events = Drain(session);
        var open = Assert.Single(events, e => e.Kind == "open");
        Assert.Equal("/app/claims?sla=breached", ((VoiceOpenEvent)open.Data).Href);
    }

    [Fact]
    public async Task A_chart_is_sent_to_the_screen_and_the_model_is_told_it_is_there()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        var session = rig.Broker.Find(started.SessionId, Alice)!;
        rig.Conversations.Next = new CommandCenterQueryResponse(
            [new TextBlock("Claims rose midweek."), new ChartBlock("Claims by day", "line", [new ChartPoint("Mon", 2), new ChartPoint("Tue", 5)], "claims", "Peak on Tuesday")],
            Guid.NewGuid(), "c", "None", "v1", "Internal", false, DateTimeOffset.UtcNow);

        var result = await rig.Toolbox.ExecuteAsync(session, "make_visual", "{\"question\":\"claims by day\"}", CancellationToken.None);

        var output = JsonDocument.Parse(result.OutputJson).RootElement;
        Assert.True(output.GetProperty("on_screen").GetBoolean());
        Assert.Contains("Mon 2; Tue 5", output.GetProperty("answer").GetString());
        var shown = Assert.Single(Drain(session), e => e.Kind == "blocks");
        Assert.Equal(2, ((VoiceBlocksEvent)shown.Data).Blocks.Count);
    }

    [Fact]
    public async Task Used_up_credits_and_failures_become_plain_reasons_never_exception_text()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        var session = rig.Broker.Find(started.SessionId, Alice)!;

        rig.Conversations.Throw = new AiCreditsExhaustedException();
        var credits = await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"x\"}", CancellationToken.None);
        Assert.Equal("credits_used_up", JsonDocument.Parse(credits.OutputJson).RootElement.GetProperty("error").GetString());

        rig.Conversations.Throw = new PermissionDeniedException(InsuranceIntelligence.Domain.Administration.Permission.ViewCommandCenter);
        var denied = await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"x\"}", CancellationToken.None);
        Assert.Equal("not_allowed", JsonDocument.Parse(denied.OutputJson).RootElement.GetProperty("error").GetString());

        rig.Conversations.Throw = new InvalidOperationException("SQL timeout in dbo.Claims at server prod-sql-01");
        var failed = await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"x\"}", CancellationToken.None);
        Assert.Equal("failed", failed.Outcome);
        Assert.DoesNotContain("SQL", failed.OutputJson);
        Assert.DoesNotContain("prod-sql", failed.OutputJson);
    }

    [Fact]
    public async Task One_call_cannot_make_unlimited_tool_calls()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        var session = rig.Broker.Find(started.SessionId, Alice)!;
        rig.Conversations.Next = Answer("ok");

        VoiceToolResult? last = null;
        for (var i = 0; i <= VoiceToolbox.MaxToolCallsPerSession; i++)
        {
            last = await rig.Toolbox.ExecuteAsync(session, "ask_analyst", "{\"question\":\"again\"}", CancellationToken.None);
        }

        Assert.Equal("refused", last!.Outcome);
        Assert.Equal(VoiceToolbox.MaxToolCallsPerSession, rig.Conversations.Requests.Count);
    }

    // ---- who may see a call -------------------------------------------------------------------

    [Fact]
    public async Task Another_person_or_another_workspace_cannot_find_or_lock_a_call()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);

        Assert.NotNull(rig.Broker.Find(started.SessionId, Alice));
        Assert.Null(rig.Broker.Find(started.SessionId, Bob));
        Assert.Null(rig.Broker.Find(started.SessionId, OtherTenantAlice));
        Assert.Null(rig.Broker.Find("not-a-session", Alice));
    }

    // ---- ending -------------------------------------------------------------------------------

    [Fact]
    public async Task When_the_link_drops_the_call_is_cleaned_up_audited_and_its_place_freed()
    {
        var rig = new Rig(new VoiceLimitOptions { MaxConcurrentSessions = 1 });
        var started = await rig.Start(Alice);

        rig.Transport.Drop();
        await Eventually(() => rig.Broker.Find(started.SessionId, Alice) is null);

        Assert.True(rig.Transport.Disposed);
        var audit = await rig.Audit.ListAsync(Alice.Scope);
        Assert.Contains(audit, e => e.ActionName == "voice.session.started");
        Assert.Contains(audit, e => e.ActionName == "voice.session.ended" && e.AuditReason!.Contains("dropped"));
        await rig.Broker.PrepareAsync(Alice, CancellationToken.None);
    }

    [Fact]
    public async Task Ending_a_call_twice_is_harmless_and_stops_the_screen_feed()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);
        var session = rig.Broker.Find(started.SessionId, Alice)!;

        await rig.Broker.EndAsync(session, "first");
        await rig.Broker.EndAsync(session, "second");

        Assert.Null(rig.Broker.Find(started.SessionId, Alice));
        Assert.True(session.Events.Reader.Completion.IsCompleted);
        var ended = (await rig.Audit.ListAsync(Alice.Scope)).Where(e => e.ActionName == "voice.session.ended").ToList();
        Assert.Single(ended);
    }

    [Fact]
    public async Task The_audit_trail_records_tools_by_name_and_outcome_and_never_what_was_said()
    {
        var rig = new Rig();
        await rig.Start(Alice);
        rig.Conversations.Next = Answer("Four claims came in today.");

        rig.Transport.Incoming(FunctionCall("c1", "ask_analyst", "{\"question\":\"secret customer name Namukasa\"}"));
        await rig.Transport.WaitForSent("function_call_output");

        var audit = await rig.Audit.ListAsync(Alice.Scope);
        var tool = Assert.Single(audit, e => e.ActionName == "voice.tool.called");
        Assert.Equal("ask_analyst: ok", tool.AuditReason);
        Assert.DoesNotContain(audit, e => (e.AuditReason ?? string.Empty).Contains("Namukasa"));
    }

    [Fact]
    public async Task Ending_through_the_tool_closes_the_call_after_the_goodbye()
    {
        var rig = new Rig();
        var started = await rig.Start(Alice);

        rig.Transport.Incoming(FunctionCall("bye", "end_conversation", "{}"));
        await rig.Transport.WaitForSent("function_call_output");

        Assert.True(rig.Broker.Find(started.SessionId, Alice)!.EndRequested);
    }

    // ---- timings ------------------------------------------------------------------------------

    [Fact]
    public void Timings_keep_only_known_numbers_and_report_percentiles_against_the_budget()
    {
        var timings = new VoiceTimings();
        for (var i = 1; i <= 100; i++)
        {
            timings.Record("tenant-a", new Dictionary<string, double> { ["first_audio"] = i * 40 });
        }

        var kept = timings.Record("tenant-a", new Dictionary<string, double>
        {
            ["secret_note"] = 5,
            ["first_audio"] = -3,
            ["connected"] = double.NaN,
            ["answer_received"] = 99_999_999,
        });

        Assert.Equal(0, kept);
        var first = timings.Summarise("tenant-a").Single(t => t.Name == "first_audio");
        Assert.Equal(100, first.Count);
        Assert.Equal(2000, first.P50Milliseconds);
        Assert.Equal(3800, first.P95Milliseconds);
        Assert.Equal(3500, first.BudgetMilliseconds);
        Assert.Equal(0, timings.Summarise("tenant-b").Single(t => t.Name == "first_audio").Count);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static CommandCenterQueryResponse Answer(string text, Guid? conversationId = null) =>
        new([new TextBlock(text)], Guid.NewGuid(), "c", "None", "v1", "Internal", false, DateTimeOffset.UtcNow) { ConversationId = conversationId };

    private static string FunctionCall(string callId, string name, string arguments) =>
        JsonSerializer.Serialize(new { type = "response.function_call_arguments.done", call_id = callId, name, arguments });

    private static List<VoiceScreenEvent> Drain(VoiceSession session)
    {
        var events = new List<VoiceScreenEvent>();
        while (session.Events.Reader.TryRead(out var item))
        {
            events.Add(item);
        }

        return events;
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The condition did not become true in time.");
    }

    private sealed class Rig
    {
        public Rig(VoiceLimitOptions? limits = null, bool configured = true, bool answers = true)
        {
            Transport = new FakeTransport { Answers = answers };
            Factory = new FakeFactory(Transport);
            Audit = new InMemoryAuditRepository();
            Conversations = new FakeConversations();
            var scopes = new FakeScopes(Conversations, Audit);
            Toolbox = new VoiceToolbox(scopes);
            Broker = new VoiceSessionBroker(
                new VoiceLiveOptions { Endpoint = configured ? "https://voice.example.test" : null },
                Factory, new VoiceLimits(limits ?? new VoiceLimitOptions()), Toolbox, scopes);
        }

        public FakeTransport Transport { get; }

        public FakeFactory Factory { get; }

        public InMemoryAuditRepository Audit { get; }

        public FakeConversations Conversations { get; }

        public VoiceToolbox Toolbox { get; }

        public VoiceSessionBroker Broker { get; }

        public Task<VoiceStarted> Start(TenantContext person) =>
            Broker.StartAsync(person, null, "offer-sdp", null, null, CancellationToken.None);
    }

    private sealed class FakeFactory(FakeTransport first) : IVoiceLiveTransportFactory
    {
        public int Created { get; private set; }

        public IVoiceLiveTransport Create()
        {
            Created++;
            // The first call uses the scripted link; a second person's call gets its own so the two do not share a socket.
            return Created == 1 ? first : new FakeTransport { Answers = first.Answers };
        }
    }

    private sealed class FakeTransport : IVoiceLiveTransport
    {
        private readonly Channel<string?> incoming = Channel.CreateUnbounded<string?>();
        private readonly List<string> sent = [];

        public bool Answers { get; init; } = true;

        public bool Reject { get; set; }

        public bool Disposed { get; private set; }

        public IReadOnlyList<string> Sent
        {
            get
            {
                lock (sent)
                {
                    return sent.ToList();
                }
            }
        }

        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendAsync(string json, CancellationToken cancellationToken)
        {
            lock (sent)
            {
                sent.Add(json);
            }

            if (json.Contains("rtc.call.sdp.create"))
            {
                if (Reject)
                {
                    incoming.Writer.TryWrite("{\"type\":\"error\",\"error\":{\"message\":\"no\"}}");
                }
                else if (Answers)
                {
                    incoming.Writer.TryWrite("{\"type\":\"rtc.call.sdp.created\",\"sdp_answer\":\"answer-sdp\"}");
                }
            }

            return Task.CompletedTask;
        }

        public async Task<string?> ReceiveAsync(CancellationToken cancellationToken) => await incoming.Reader.ReadAsync(cancellationToken);

        public void Incoming(string json) => incoming.Writer.TryWrite(json);

        public void Drop() => incoming.Writer.TryWrite(null);

        public async Task WaitForSent(string text, int count = 1)
        {
            for (var i = 0; i < 300; i++)
            {
                if (Sent.Count(m => m.Contains(text)) >= count)
                {
                    return;
                }

                await Task.Delay(10);
            }

            Assert.Fail($"The link never sent '{text}' ({count}). Sent: {string.Join(" | ", Sent.Select(m => m.Length > 80 ? m[..80] : m))}");
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeConversations : ICommandCenterConversationService
    {
        public List<CommandCenterQueryRequest> Requests { get; } = [];

        public List<TenantContext> Contexts { get; } = [];

        public CommandCenterQueryResponse Next { get; set; } = Answer("Done.");

        public Exception? Throw { get; set; }

        public TenantContext? Current { get; set; }

        public Task<CommandCenterQueryResponse> QueryAsync(CommandCenterQueryRequest request, string correlationId, CancellationToken cancellationToken = default)
        {
            if (Throw is not null)
            {
                return Task.FromException<CommandCenterQueryResponse>(Throw);
            }

            Requests.Add(request);
            Contexts.Add(Current!);
            return Task.FromResult(Next);
        }

        public Task<ConversationListResponse> ListAsync(bool archived, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationDetailDto> GetAsync(Guid conversationId, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationSummaryDto> RenameAsync(Guid conversationId, RenameConversationRequest request, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationSummaryDto> ArchiveAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationSummaryDto> RestoreAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeProfiles : IAgentBehaviourProfileRepository
    {
        public Task<AgentBehaviourProfile?> GetCurrentAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) => Task.FromResult<AgentBehaviourProfile?>(null);
        public Task<IReadOnlyCollection<AgentBehaviourProfile>> ListVersionsAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AgentBehaviourProfile>>([]);
        public Task<IReadOnlyCollection<AgentBehaviourProfile>> ListCurrentForAllAgentsAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AgentBehaviourProfile>>([]);
        public Task SaveAsync(AgentBehaviourProfile version, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeScopes(FakeConversations conversations, IAuditRepository audit) : IVoiceScopeFactory
    {
        public IVoiceScope Create(TenantContext context)
        {
            conversations.Current = context;
            return new Scope(conversations, audit);
        }

        private sealed class Scope(FakeConversations conversations, IAuditRepository audit) : IVoiceScope
        {
            public ICommandCenterConversationService Conversations => conversations;
            public IAgentBehaviourProfileRepository Profiles { get; } = new FakeProfiles();
            public IAuditRepository Audit => audit;
            public void Dispose() { }
        }
    }
}
