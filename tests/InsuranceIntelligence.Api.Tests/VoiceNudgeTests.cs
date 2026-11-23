using InsuranceIntelligence.Application.Brief;
using InsuranceIntelligence.Application.Voice;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>When a spoken nudge may be offered, and what it may say.</summary>
public sealed class VoiceNudgeTests
{
    private static NudgeCandidate Item(string key) => new(key, "Text " + key, "private", "/app/claims/" + key);

    private static VoiceNudgeLedger Ledger(FakeClock clock, VoiceNudgeOptions? options = null) => new(options ?? new VoiceNudgeOptions(), clock);

    [Fact]
    public void A_nudge_is_offered_in_working_hours_and_only_once()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero)); // Monday, midday in Kampala
        var ledger = Ledger(clock);

        var first = ledger.TryClaim("alice", [Item("a"), Item("b")]);
        var second = ledger.TryClaim("alice", [Item("a"), Item("b")]);

        Assert.Equal("a", first?.Key);
        Assert.Equal("b", second?.Key);
        Assert.Null(ledger.TryClaim("alice", [Item("a"), Item("b")]));
    }

    [Fact]
    public void At_most_three_a_day_for_one_person_and_the_next_day_starts_again()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 23, 6, 0, 0, TimeSpan.Zero));
        var ledger = Ledger(clock, new VoiceNudgeOptions { RepeatDays = 1 });
        var all = new[] { Item("a"), Item("b"), Item("c"), Item("d"), Item("e") };

        Assert.NotNull(ledger.TryClaim("alice", all));
        Assert.NotNull(ledger.TryClaim("alice", all));
        Assert.NotNull(ledger.TryClaim("alice", all));
        Assert.Null(ledger.TryClaim("alice", all));
        Assert.NotNull(ledger.TryClaim("bob", all)); // another person has their own allowance

        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal("d", ledger.TryClaim("alice", all)?.Key);
    }

    [Theory]
    [InlineData(2026, 11, 23, 4, 30)] // 07:30 in Kampala, before work
    [InlineData(2026, 11, 23, 15, 0)] // 18:00 in Kampala, after work
    [InlineData(2026, 11, 21, 9, 0)] // a Saturday
    [InlineData(2026, 11, 22, 9, 0)] // a Sunday
    public void Nothing_is_offered_outside_working_hours_or_at_the_weekend(int year, int month, int day, int hour, int minute)
    {
        var ledger = Ledger(new FakeClock(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero)));

        Assert.Null(ledger.TryClaim("alice", [Item("a")]));
    }

    [Fact]
    public void The_same_thing_is_not_repeated_within_three_days_but_is_after()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        var ledger = Ledger(clock);

        Assert.NotNull(ledger.TryClaim("alice", [Item("a")]));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Null(ledger.TryClaim("alice", [Item("a")]));
        clock.Advance(TimeSpan.FromDays(2) + TimeSpan.FromHours(1)); // a working day, just over three days since it was offered
        Assert.NotNull(ledger.TryClaim("alice", [Item("a")]));
    }

    [Fact]
    public async Task Requests_at_the_same_moment_make_one_nudge_between_them()
    {
        var ledger = Ledger(new FakeClock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero)));

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() => ledger.TryClaim("alice", [Item("only")]))));

        Assert.Single(results, result => result is not null);
    }

    [Fact]
    public async Task A_locked_phone_hears_no_names_and_no_figures_and_gets_no_link()
    {
        var nudges = new VoiceNudges(
            new StubBrief(new BriefResponse(
                [new BriefItemDto("c1", "Grace Nakato", "POL-1", "Motor", "Open", "UGX 4,500,000 is waiting on your decision.", "Open claim", "/app/claims/c1")],
                [],
                new BriefRetentionSummaryDto(3, 12_000_000m, "UGX"))),
            Ledger(new FakeClock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero))));

        var locked = await nudges.NextAsync("alice", locked: true, CancellationToken.None);

        Assert.NotNull(locked);
        Assert.True(locked!.Private);
        Assert.Null(locked.Href);
        Assert.DoesNotContain("Grace", locked.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("UGX", locked.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(char.Parse("\u2014").ToString(), locked.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unlocked_phone_hears_what_the_brief_says_with_a_link_inside_IIP_only()
    {
        var nudges = new VoiceNudges(
            new StubBrief(new BriefResponse(
                [
                    new BriefItemDto("c1", "Grace Nakato", "POL-1", "Motor", "Open", "Past its deadline.", "Open claim", "https://elsewhere.example/phish"),
                    new BriefItemDto("c2", "Peter Okello", "POL-2", "Medical", "Open", "Waiting for a document.", "Open claim", "/app/claims/c2"),
                ],
                [],
                null)),
            Ledger(new FakeClock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero))));

        var first = await nudges.NextAsync("alice", locked: false, CancellationToken.None);
        var second = await nudges.NextAsync("alice", locked: false, CancellationToken.None);

        Assert.Contains("Grace Nakato", first!.Text, StringComparison.Ordinal);
        Assert.Null(first.Href); // an address outside IIP is never offered
        Assert.False(first.Private);
        Assert.Equal("/app/claims/c2", second!.Href);
    }

    [Fact]
    public async Task A_quiet_brief_makes_no_nudge()
    {
        var nudges = new VoiceNudges(new StubBrief(new BriefResponse([], [], null)), Ledger(new FakeClock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero))));

        Assert.Null(await nudges.NextAsync("alice", locked: false, CancellationToken.None));
    }

    private sealed class StubBrief(BriefResponse response) : IBriefService
    {
        public Task<BriefResponse> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(response);
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }
}
