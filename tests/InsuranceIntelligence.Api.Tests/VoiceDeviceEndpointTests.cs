using InsuranceIntelligence.Api.Voice;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Voice;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The two anonymous endpoints a phone calls while the app is closed: they answer only to a known device credential.</summary>
public sealed class VoiceDeviceEndpointTests
{
    private static readonly TenantContext Alice = new("tenant-a", "ws-1", "alice", ["ClaimsManager"]);

    private sealed class StubNudges(Nudge? nudge) : IVoiceNudges
    {
        public string? Person;
        public bool? Locked;

        public Task<Nudge?> NextAsync(string person, bool locked, CancellationToken cancellationToken)
        {
            Person = person;
            Locked = locked;
            return Task.FromResult(nudge);
        }
    }

    private sealed class StubChecker(bool configured, WakeCheck? result) : IWakeChecker
    {
        public int Calls;
        public bool IsConfigured => configured;

        public Task<WakeCheck?> CheckAsync(byte[] wav, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private static DefaultHttpContext Context(string? token, byte[]? audio = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "device-test" };
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        if (token is not null)
        {
            context.Request.Headers["X-Device-Check"] = token;
        }

        if (audio is not null)
        {
            context.Request.ContentType = "multipart/form-data; boundary=wake";
            var files = new FormFileCollection
            {
                new FormFile(new MemoryStream(audio), 0, audio.Length, "audio", "wake.wav") { Headers = new HeaderDictionary(), ContentType = "audio/wav" },
            };
            context.Request.Form = new FormCollection([], files);
        }

        return context;
    }

    private static async Task<(int Status, string Body)> Run(IResult result)
    {
        var context = Context(null);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    private static Task<IResult> Nudge(IDeviceChecks checks, IVoiceNudges nudges, string? token, bool? locked = null) =>
        VoiceEndpoints.DeviceNudge(locked, checks, nudges, new TenantContextAccessor(), Context(token), CancellationToken.None);

    private static Task<IResult> Wake(IDeviceChecks checks, IWakeChecker checker, string? token, byte[]? audio) =>
        VoiceEndpoints.DeviceWake(checks, checker, Context(token, audio), CancellationToken.None);

    [Fact]
    public async Task The_nudge_check_refuses_a_missing_or_unknown_credential()
    {
        var checks = new DeviceChecks();
        var nudges = new StubNudges(new Nudge("n1", "text", null, false));

        Assert.Equal(401, (await Run(await Nudge(checks, nudges, null))).Status);
        Assert.Equal(401, (await Run(await Nudge(checks, nudges, "not-a-credential"))).Status);
        Assert.Null(nudges.Person);
    }

    [Fact]
    public async Task The_nudge_check_answers_for_the_person_the_credential_was_issued_to()
    {
        var checks = new DeviceChecks();
        var token = checks.Issue(Alice).Token;
        var nudges = new StubNudges(new Nudge("n1", "A claim is past its deadline.", "/app/claims/c1", false));

        var (status, body) = await Run(await Nudge(checks, nudges, token, locked: true));

        Assert.Equal(200, status);
        Assert.Contains("past its deadline", body);
        Assert.Equal("tenant-a|alice", nudges.Person);
        Assert.True(nudges.Locked);
    }

    [Fact]
    public async Task The_nudge_check_says_nothing_when_there_is_nothing_to_say()
    {
        var checks = new DeviceChecks();
        var token = checks.Issue(Alice).Token;

        Assert.Equal(204, (await Run(await Nudge(checks, new StubNudges(null), token))).Status);
    }

    [Fact]
    public async Task The_nudge_check_is_held_to_six_a_minute()
    {
        var checks = new DeviceChecks();
        var token = checks.Issue(Alice).Token;
        var nudges = new StubNudges(null);

        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(204, (await Run(await Nudge(checks, nudges, token))).Status);
        }

        Assert.Equal(429, (await Run(await Nudge(checks, nudges, token))).Status);
    }

    [Fact]
    public async Task The_wake_check_refuses_a_missing_or_unknown_credential_without_listening()
    {
        var checks = new DeviceChecks();
        var checker = new StubChecker(true, new WakeCheck(true, "Hey Kato"));
        var audio = new byte[8000];

        Assert.Equal(401, (await Run(await Wake(checks, checker, null, audio))).Status);
        Assert.Equal(401, (await Run(await Wake(checks, checker, "nope", audio))).Status);
        Assert.Equal(0, checker.Calls);
    }

    [Fact]
    public async Task The_wake_check_reports_what_was_heard()
    {
        var checks = new DeviceChecks();
        var token = checks.Issue(Alice).Token;
        var checker = new StubChecker(true, new WakeCheck(true, "Hey Kato."));

        var (status, body) = await Run(await Wake(checks, checker, token, new byte[8000]));

        Assert.Equal(200, status);
        Assert.Contains("\"wake\":true", body);
        Assert.Contains("Hey Kato", body);
        Assert.Equal(1, checker.Calls);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500_000)]
    public async Task The_wake_check_refuses_a_clip_that_is_too_short_or_too_long(int length)
    {
        var checks = new DeviceChecks();
        var token = checks.Issue(Alice).Token;
        var checker = new StubChecker(true, new WakeCheck(true, "Hey Kato"));

        Assert.Equal(400, (await Run(await Wake(checks, checker, token, new byte[length]))).Status);
        Assert.Equal(0, checker.Calls);
    }

    [Fact]
    public async Task The_wake_check_says_when_voice_is_not_set_up_and_when_the_check_fails()
    {
        var checks = new DeviceChecks();
        var token = checks.Issue(Alice).Token;

        Assert.Equal(503, (await Run(await Wake(checks, new StubChecker(false, null), token, new byte[8000]))).Status);
        Assert.Equal(502, (await Run(await Wake(checks, new StubChecker(true, null), token, new byte[8000]))).Status);
    }

    [Fact]
    public async Task The_wake_check_is_held_to_twenty_a_minute()
    {
        var checks = new DeviceChecks();
        var token = checks.Issue(Alice).Token;
        var checker = new StubChecker(true, new WakeCheck(false, "hello"));

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(200, (await Run(await Wake(checks, checker, token, new byte[8000]))).Status);
        }

        Assert.Equal(429, (await Run(await Wake(checks, checker, token, new byte[8000]))).Status);
    }
}
