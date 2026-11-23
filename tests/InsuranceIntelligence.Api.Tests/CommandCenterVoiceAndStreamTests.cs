using System.Text;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Voice;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CommandCenterVoiceAndStreamTests
{
    private const string Tenant = "tenant-demo";
    private const string User = "user-1";

    // ---- limits -------------------------------------------------------------------------------

    [Fact]
    public void A_person_is_held_to_the_transcriptions_allowed_in_a_minute()
    {
        var clock = new FakeTime();
        var limits = new VoiceLimits(new VoiceLimitOptions { TranscriptionsPerMinute = 2 }, clock);

        Assert.True(limits.TryUseTranscription(Tenant, User, 5).Allowed);
        Assert.True(limits.TryUseTranscription(Tenant, User, 5).Allowed);
        var third = limits.TryUseTranscription(Tenant, User, 5);
        Assert.False(third.Allowed);
        Assert.Equal("rate_limited", third.Code);

        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.True(limits.TryUseTranscription(Tenant, User, 5).Allowed);
    }

    [Fact]
    public void The_daily_allowance_runs_out_and_returns_the_next_day()
    {
        var clock = new FakeTime();
        var limits = new VoiceLimits(new VoiceLimitOptions { AudioSecondsPerDay = 60 }, clock);

        Assert.True(limits.TryUseTranscription(Tenant, User, 50).Allowed);
        var over = limits.TryUseTranscription(Tenant, User, 20);
        Assert.False(over.Allowed);
        Assert.Equal("daily_limit", over.Code);

        clock.Advance(TimeSpan.FromHours(25));
        Assert.True(limits.TryUseTranscription(Tenant, User, 20).Allowed);
    }

    [Fact]
    public void Limits_are_per_person_and_per_tenant()
    {
        var limits = new VoiceLimits(new VoiceLimitOptions { TranscriptionsPerMinute = 1 }, new FakeTime());

        Assert.True(limits.TryUseTranscription(Tenant, "a", 1).Allowed);
        Assert.True(limits.TryUseTranscription(Tenant, "b", 1).Allowed);
        Assert.True(limits.TryUseTranscription("other-tenant", "a", 1).Allowed);
        Assert.False(limits.TryUseTranscription(Tenant, "a", 1).Allowed);
    }

    [Fact]
    public void Live_calls_are_capped_and_a_place_is_freed_when_the_call_ends()
    {
        var limits = new VoiceLimits(new VoiceLimitOptions { MaxConcurrentSessions = 1 }, new FakeTime());

        var first = limits.TryOpenSession(Tenant, User, out var okDecision);
        Assert.NotNull(first);
        Assert.True(okDecision.Allowed);

        Assert.Null(limits.TryOpenSession(Tenant, User, out var refused));
        Assert.Equal("too_many_calls", refused.Code);

        first!.Dispose();
        first.Dispose(); // ending twice must not free a second place
        using var again = limits.TryOpenSession(Tenant, User, out _);
        Assert.NotNull(again);
        Assert.Null(limits.TryOpenSession(Tenant, User, out _));
    }

    [Fact]
    public void Call_minutes_count_against_the_day()
    {
        var limits = new VoiceLimits(new VoiceLimitOptions { AudioSecondsPerDay = 100 }, new FakeTime());
        limits.AddCallSeconds(Tenant, User, 100);

        Assert.Null(limits.TryOpenSession(Tenant, User, out var decision));
        Assert.Equal("daily_limit", decision.Code);
        Assert.False(limits.TryUseTranscription(Tenant, User, 1).Allowed);
    }

    [Fact]
    public void Wav_length_is_read_from_its_size()
    {
        // 16 kHz, 16-bit, mono = 32,000 bytes a second, after a 44 byte header.
        Assert.Equal(2.0, VoiceLimits.EstimateSeconds(44 + 64_000, "audio/wav"), 3);
        Assert.Equal(0.0, VoiceLimits.EstimateSeconds(10, "audio/wav"));
        Assert.True(VoiceLimits.EstimateSeconds(40_000, "audio/webm") >= 9.9);
    }

    // ---- voice note endpoint -----------------------------------------------------------------

    [Fact]
    public async Task A_voice_note_comes_back_as_words_with_no_em_dash()
    {
        var transcriber = new StubTranscriber { Text = "Show claims \u2014 open ones" };
        var (status, body) = await RunTranscribe(transcriber, new VoiceLimits(new VoiceLimitOptions(), new FakeTime()), audio: new byte[1000]);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("Show claims", body);
        Assert.DoesNotContain('\u2014', body);
        Assert.Equal(1, transcriber.Calls);
    }

    [Fact]
    public async Task Nothing_is_transcribed_when_no_speech_provider_is_connected()
    {
        var transcriber = new StubTranscriber { Available = false };
        var (status, body) = await RunTranscribe(transcriber, new VoiceLimits(new VoiceLimitOptions(), new FakeTime()), audio: new byte[1000]);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Contains("not_configured", body);
        Assert.Equal(0, transcriber.Calls);
    }

    [Fact]
    public async Task Too_much_voice_is_refused_before_the_provider_is_called()
    {
        var transcriber = new StubTranscriber();
        var limits = new VoiceLimits(new VoiceLimitOptions { TranscriptionsPerMinute = 1 }, new FakeTime());

        Assert.Equal(StatusCodes.Status200OK, (await RunTranscribe(transcriber, limits, new byte[1000])).Status);
        var (status, body) = await RunTranscribe(transcriber, limits, new byte[1000]);

        Assert.Equal(StatusCodes.Status429TooManyRequests, status);
        Assert.Contains("rate_limited", body);
        Assert.Equal(1, transcriber.Calls);
    }

    [Fact]
    public async Task An_empty_or_oversized_recording_is_refused()
    {
        var transcriber = new StubTranscriber();
        var limits = new VoiceLimits(new VoiceLimitOptions(), new FakeTime());

        Assert.Equal(StatusCodes.Status400BadRequest, (await RunTranscribe(transcriber, limits, [])).Status);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, (await RunTranscribe(transcriber, limits, new byte[9 * 1024 * 1024])).Status);
        Assert.Equal(0, transcriber.Calls);
    }

    [Fact]
    public async Task Words_that_cannot_be_made_out_are_reported_not_invented()
    {
        var transcriber = new StubTranscriber { Text = "   " };
        var (status, body) = await RunTranscribe(transcriber, new VoiceLimits(new VoiceLimitOptions(), new FakeTime()), new byte[1000]);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
        Assert.Contains("not_understood", body);
    }

    [Fact]
    public async Task Status_says_whether_this_workspace_can_hear()
    {
        foreach (var available in new[] { true, false })
        {
            var context = Context();
            var result = await CommandCenterVoiceEndpoints.Status(
                new StubTranscriber { Available = available }, Tenant1(), Tenant1(), new InfrastructureOptions { AuthMode = "Development" }, context, CancellationToken.None);
            var (_, body) = await Execute(result);
            Assert.Contains($"\"available\":{available.ToString().ToLowerInvariant()}", body);
        }
    }

    [Theory]
    [InlineData("sw", SupportedLanguageCode.Sw)]
    [InlineData("LG", SupportedLanguageCode.Lg)]
    [InlineData(" en ", SupportedLanguageCode.En)]
    public void Language_codes_are_read_loosely(string value, SupportedLanguageCode expected) =>
        Assert.Equal(expected, CommandCenterVoiceEndpoints.ParseLanguage(value));

    [Fact]
    public void An_unknown_language_leaves_the_provider_to_decide() =>
        Assert.Null(CommandCenterVoiceEndpoints.ParseLanguage("xx"));

    // ---- spoken replies in Luganda and Swahili -------------------------------------------------

    [Fact]
    public async Task A_reply_is_translated_and_then_spoken_in_the_chosen_language()
    {
        var translation = new StubTranslation { Result = TranslationOutcome.Success("Ebibuuzo bina byazze leero.", fromCache: false) };
        var speaker = new StubSpeaker { Audio = [1, 2, 3] };

        var (status, body) = await RunSpeak(new VoiceReplyRequest("Four claims came in today.", "lg"), speaker, translation);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("Ebibuuzo bina byazze leero.", body);
        Assert.Contains("\"translated\":true", body);
        Assert.Contains(Convert.ToBase64String([1, 2, 3]), body);
        Assert.Equal(SupportedLanguageCode.Lg, speaker.Language);
        Assert.Equal("Ebibuuzo bina byazze leero.", speaker.Spoken);
    }

    [Fact]
    public async Task A_reply_that_cannot_be_translated_is_never_spoken_in_a_foreign_voice()
    {
        var translation = new StubTranslation { Result = TranslationOutcome.Fallback("Four claims came in today.", TranslationFailureReason.ProviderNotConfigured) };
        var speaker = new StubSpeaker { Audio = [9] };

        var (status, body) = await RunSpeak(new VoiceReplyRequest("Four claims came in today.", "sw"), speaker, translation);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("\"translated\":false", body);
        Assert.Contains("\"audio\":null", body);
        Assert.Null(speaker.Spoken);
    }

    [Theory]
    [InlineData("", "lg")]
    [InlineData("Hello", "en")]
    [InlineData("Hello", "xx")]
    public async Task Text_that_is_empty_or_in_a_language_not_spoken_here_is_refused(string text, string language)
    {
        var (status, _) = await RunSpeak(new VoiceReplyRequest(text, language), new StubSpeaker(), new StubTranslation());
        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task Spoken_replies_count_against_the_same_voice_limits()
    {
        var limits = new VoiceLimits(new VoiceLimitOptions { TranscriptionsPerMinute = 1 }, new FakeTime());
        var speaker = new StubSpeaker { Audio = [1] };
        var translation = new StubTranslation { Result = TranslationOutcome.Success("Bino.", false) };

        Assert.Equal(StatusCodes.Status200OK, (await RunSpeak(new VoiceReplyRequest("Hello", "lg"), speaker, translation, limits)).Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (await RunSpeak(new VoiceReplyRequest("Hello", "lg"), speaker, translation, limits)).Status);
    }

    // ---- streaming query ---------------------------------------------------------------------

    [Fact]
    public async Task The_stream_carries_the_steps_then_the_answer_then_done_in_order()
    {
        var progress = new AgentProgress();
        var service = new StubConversationService(async (_, _) =>
        {
            progress.Report("step", "Reading your question");
            progress.Report("tool", "Counting claims (today)");
            await Task.Yield();
            return Response("Four claims today.");
        });

        var text = await RunStream(service, progress);

        var order = new[] { "event: step", "event: step", "event: tool", "event: answer", "event: done" }.Select(e => text.IndexOf(e, StringComparison.Ordinal)).ToArray();
        Assert.All(order, index => Assert.True(index >= 0, text));
        Assert.Equal(order.OrderBy(i => i).ToArray(), order);
        Assert.Contains("Checking your access", text);
        Assert.Contains("Counting claims (today)", text);
        Assert.Contains("Four claims today.", text);
    }

    [Fact]
    public async Task A_refused_question_ends_the_stream_with_an_error_not_a_crash()
    {
        var service = new StubConversationService((_, _) => throw new PermissionDeniedException(Permission.ViewCommandCenter));

        var text = await RunStream(service, new AgentProgress());

        Assert.Contains("event: error", text);
        Assert.Contains("forbidden", text);
        Assert.Contains("event: done", text);
        Assert.DoesNotContain("event: answer", text);
    }

    [Fact]
    public async Task A_bad_question_reports_the_field_that_was_wrong()
    {
        var service = new StubConversationService((_, _) => throw new CommandCenterValidationException("question", "Ask a question first."));

        var text = await RunStream(service, new AgentProgress());

        Assert.Contains("event: error", text);
        Assert.Contains("\"field\":\"question\"", text);
    }

    [Fact]
    public async Task Used_up_credits_come_back_as_an_answer_the_person_can_read()
    {
        var service = new StubConversationService((_, _) => throw new InsuranceIntelligence.Application.Billing.AiCreditsExhaustedException());

        var text = await RunStream(service, new AgentProgress());

        Assert.Contains("event: answer", text);
        Assert.Contains("AI credits for this month are used up", text);
        Assert.DoesNotContain("event: error", text);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static TenantContextAccessor Tenant1()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetCurrent(new TenantContext(Tenant, "workspace-claims", User, ["ClaimsManager"]));
        return accessor;
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "voice-test" };
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int Status, string Body)> RunTranscribe(StubTranscriber transcriber, IVoiceLimits limits, byte[] audio)
    {
        var context = Context();
        context.Request.ContentType = "multipart/form-data; boundary=voice";
        var files = new FormFileCollection();
        if (audio.Length > 0)
        {
            files.Add(new FormFile(new MemoryStream(audio), 0, audio.Length, "audio", "voice-note.wav")
            {
                Headers = new HeaderDictionary(),
                ContentType = "audio/wav",
            });
        }

        context.Request.Form = new FormCollection([], files);
        var accessor = Tenant1();
        var result = await CommandCenterVoiceEndpoints.Transcribe(
            transcriber, limits, accessor, accessor, new InfrastructureOptions { AuthMode = "Development" }, context, CancellationToken.None);
        return await Execute(result);
    }

    private static async Task<(int Status, string Body)> Execute(IResult result)
    {
        var context = Context();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    private static async Task<string> RunStream(StubConversationService service, AgentProgress progress)
    {
        var context = Context();
        var accessor = Tenant1();
        await CommandCenterQueryEndpoints.RunQueryStream(
            new CommandCenterQueryRequest("How many claims today?"),
            service, progress, accessor, new StubPermissions(), new InfrastructureOptions { AuthMode = "Development" },
            Options.Create(new JsonOptions()), context, CancellationToken.None);
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
    }

    private static async Task<(int Status, string Body)> RunSpeak(VoiceReplyRequest request, StubSpeaker speaker, StubTranslation translation, IVoiceLimits? limits = null)
    {
        var accessor = Tenant1();
        var result = await CommandCenterVoiceEndpoints.Speak(
            request, speaker, translation, limits ?? new VoiceLimits(new VoiceLimitOptions(), new FakeTime()), accessor, accessor,
            new InfrastructureOptions { AuthMode = "Development" }, Context(), CancellationToken.None);
        return await Execute(result);
    }

    private sealed class StubSpeaker : IVoiceNoteSpeaker
    {
        public byte[]? Audio { get; init; }
        public string? Spoken { get; private set; }
        public SupportedLanguageCode? Language { get; private set; }

        public Task<byte[]?> SpeakAsync(TenantScope scope, string text, SupportedLanguageCode language, CancellationToken cancellationToken = default)
        {
            Spoken = text;
            Language = language;
            return Task.FromResult(Audio);
        }
    }

    private sealed class StubTranslation : ITranslationService
    {
        public TranslationOutcome Result { get; init; } = TranslationOutcome.NotNeeded("x");

        public Task<TranslationOutcome> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default, IReadOnlyCollection<string>? protectedLiterals = null) =>
            Task.FromResult(Result);
    }

    private static CommandCenterQueryResponse Response(string text) =>
        new([new TextBlock(text)], Guid.NewGuid(), "corr", "None", "test-v1", "Internal", false, DateTimeOffset.UtcNow);

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class StubTranscriber : IStaffVoiceTranscriber
    {
        public bool Available { get; init; } = true;
        public string Text { get; init; } = "show me open claims";
        public int Calls { get; private set; }

        public Task<bool> IsStaffTranscriptionAvailableAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(Available);

        public Task<string?> TranscribeAudioAsync(TenantScope scope, byte[] audio, string? contentType, SupportedLanguageCode? language, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<string?>(Text);
        }
    }

    private sealed class StubPermissions : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubConversationService(Func<CommandCenterQueryRequest, string, Task<CommandCenterQueryResponse>> query) : ICommandCenterConversationService
    {
        public Task<CommandCenterQueryResponse> QueryAsync(CommandCenterQueryRequest request, string correlationId, CancellationToken cancellationToken = default) => query(request, correlationId);
        public Task<ConversationListResponse> ListAsync(bool archived, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationDetailDto> GetAsync(Guid conversationId, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationSummaryDto> RenameAsync(Guid conversationId, RenameConversationRequest request, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationSummaryDto> ArchiveAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConversationSummaryDto> RestoreAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
