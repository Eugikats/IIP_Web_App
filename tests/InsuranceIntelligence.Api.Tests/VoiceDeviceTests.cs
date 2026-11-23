using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Voice;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The narrow credential a phone uses while the app is closed, and the wake phrase check.</summary>
public sealed class VoiceDeviceTests
{
    private static readonly TenantContext Alice = new("tenant-a", "ws-1", "alice", ["ClaimsManager"]);
    private static readonly TenantContext Bob = new("tenant-a", "ws-1", "bob", ["ClaimsManager"]);

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    [Fact]
    public void A_token_belongs_to_the_person_it_was_issued_to_and_nobody_else()
    {
        var checks = new DeviceChecks();
        var alice = checks.Issue(Alice);
        var bob = checks.Issue(Bob);

        Assert.Equal("alice", checks.Resolve(alice.Token)?.UserId);
        Assert.Equal("bob", checks.Resolve(bob.Token)?.UserId);
        Assert.NotEqual(alice.Token, bob.Token);
        Assert.Null(checks.Resolve(null));
        Assert.Null(checks.Resolve(""));
        Assert.Null(checks.Resolve("not-a-token"));
        Assert.Null(checks.Resolve(new string('x', 500)));
    }

    [Fact]
    public void A_revoked_token_stops_working_at_once()
    {
        var checks = new DeviceChecks();
        var grant = checks.Issue(Alice);

        checks.Revoke(grant.Token);

        Assert.Null(checks.Resolve(grant.Token));
    }

    [Fact]
    public void A_token_runs_out_fourteen_days_after_it_was_last_used()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        var checks = new DeviceChecks(clock);
        var grant = checks.Issue(Alice);

        clock.Advance(TimeSpan.FromDays(10));
        Assert.NotNull(checks.Resolve(grant.Token)); // used, so renewed for another fourteen days
        clock.Advance(TimeSpan.FromDays(10));
        Assert.NotNull(checks.Resolve(grant.Token));
        clock.Advance(TimeSpan.FromDays(15));
        Assert.Null(checks.Resolve(grant.Token));
    }

    [Fact]
    public void A_person_keeps_at_most_three_and_the_oldest_goes()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        var checks = new DeviceChecks(clock);
        var first = checks.Issue(Alice);
        clock.Advance(TimeSpan.FromMinutes(1));
        var second = checks.Issue(Alice);
        clock.Advance(TimeSpan.FromMinutes(1));
        var third = checks.Issue(Alice);
        clock.Advance(TimeSpan.FromMinutes(1));
        var fourth = checks.Issue(Alice);

        Assert.Null(checks.Resolve(first.Token));
        Assert.NotNull(checks.Resolve(second.Token));
        Assert.NotNull(checks.Resolve(third.Token));
        Assert.NotNull(checks.Resolve(fourth.Token));
    }

    [Fact]
    public void Each_check_has_its_own_allowance_a_minute_and_it_comes_back()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        var checks = new DeviceChecks(clock);
        var token = checks.Issue(Alice).Token;

        for (var i = 0; i < 20; i++)
        {
            Assert.True(checks.TryUse(token, "wake", 20));
        }

        Assert.False(checks.TryUse(token, "wake", 20));
        Assert.True(checks.TryUse(token, "nudge", 6)); // a different check is counted on its own
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.True(checks.TryUse(token, "wake", 20));
    }

    [Theory]
    [InlineData("Hey Kato", true)]
    [InlineData("Hey, Kato.", true)]
    [InlineData("hay cato", true)]
    [InlineData("Hi Kato, what is overdue?", true)]
    [InlineData("Okay Kato", true)]
    [InlineData("A Kato", true)]
    [InlineData("Kato.", true)]
    [InlineData("Hit Kato.", true)]
    [InlineData("Hey, at last", false)]
    [InlineData("Ask Kato later today about the claim", false)]
    [InlineData("I spoke to Kato yesterday", false)]
    [InlineData("Hey Kate", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_wake_phrase_wakes_the_phone(string? heard, bool wakes)
    {
        Assert.Equal(wakes, WakePhrase.Matches(heard));
    }
}
