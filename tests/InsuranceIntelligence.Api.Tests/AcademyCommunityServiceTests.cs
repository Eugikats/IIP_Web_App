using System.Collections.Concurrent;
using System.Text.Json;
using InsuranceIntelligence.Api.Academy;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The shared community's rules: consent first, a group to post in, no personal data, reports that hide, operator-only controls.</summary>
public sealed class AcademyCommunityServiceTests
{
    private sealed class MemoryStore : IAcademyRecordStore
    {
        private readonly ConcurrentDictionary<(string, string, string), AcademyRecord> rows = new();

        public Task<AcademyRecord?> GetAsync(string kind, string owner, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.TryGetValue((kind, owner, key), out var row) ? row : null);

        public Task SaveAsync(string kind, string owner, string key, string json, CancellationToken cancellationToken = default)
        {
            rows[(kind, owner, key)] = new AcademyRecord(kind, owner, key, json, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AcademyRecord>> ListByOwnerAsync(string kind, string owner, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AcademyRecord>>(rows.Values.Where(row => row.Kind == kind && row.Owner == owner).ToList());

        public Task<IReadOnlyList<AcademyRecord>> ListByKindAsync(string kind, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AcademyRecord>>(rows.Values.Where(row => row.Kind == kind).OrderByDescending(row => row.UpdatedAt).Take(take).ToList());

        public Task DeleteAsync(string kind, string owner, string key, CancellationToken cancellationToken = default)
        {
            rows.TryRemove((kind, owner, key), out _);
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(string kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.Values.Count(row => row.Kind == kind));
    }

    private static AcademyCommunityService NewService()
    {
        var store = new MemoryStore();
        return new AcademyCommunityService(store, new AcademyLearnerService(store, new AcademyContentProvider(), TimeProvider.System), TimeProvider.System);
    }

    private static AcademyLearner Person(string id, string name, bool op = false) => new(id, name, $"{id}@example.test", false, op);

    private static JsonElement J(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task<AcademyRuleException> Refused(Func<Task> action) => await Assert.ThrowsAsync<AcademyRuleException>(action);

    private static async Task Ready(AcademyCommunityService service, AcademyLearner learner)
    {
        await service.GiveConsentAsync(learner, default);
        await service.JoinAsync(learner, "g-claims", true, default);
    }

    [Fact]
    public async Task PostingNeedsConsentFirst_ThenAGroup_ThenCleanText()
    {
        var service = NewService();
        var aisha = Person("aisha", "Aisha");

        Assert.Equal("consent", (await Refused(() => service.PostAsync(aisha, "Hello", "Claims", default))).Code);
        await service.GiveConsentAsync(aisha, default);
        Assert.Equal("no_group", (await Refused(() => service.PostAsync(aisha, "Hello", "Claims", default))).Code);
        await service.JoinAsync(aisha, "g-claims", true, default);

        Assert.Equal(400, (await Refused(() => service.PostAsync(aisha, "   ", "Claims", default))).Status);
        Assert.Equal(400, (await Refused(() => service.PostAsync(aisha, "Hello", "Gossip", default))).Status);
        Assert.Equal("personal_data", (await Refused(() => service.PostAsync(aisha, "Call the claimant on 0772 123 456 today", "Claims", default))).Code);
        Assert.Equal(400, (await Refused(() => service.PostAsync(aisha, new string('a', AcademyCommunityService.MaxPostLength + 1), "Claims", default))).Status);

        await service.PostAsync(aisha, "We call every motor claimant within a day.", "Claims", default);
        var posts = J(await service.StateAsync(aisha, default)).GetProperty("posts").EnumerateArray().ToList();
        var post = Assert.Single(posts);
        Assert.Equal("Aisha", post.GetProperty("author").GetString());
        Assert.True(post.GetProperty("mine").GetBoolean());
    }

    [Fact]
    public async Task Groups_CountRealMembers_AndCanBeLeft()
    {
        var service = NewService();
        var a = Person("a", "Ann");
        var b = Person("b", "Ben");
        await service.JoinAsync(a, "g-fraud", true, default);
        await service.JoinAsync(b, "g-fraud", true, default);

        Group(J(await service.StateAsync(a, default)), "g-fraud", out var members, out var joined);
        Assert.Equal(2, members);
        Assert.True(joined);

        await service.JoinAsync(a, "g-fraud", false, default);
        Group(J(await service.StateAsync(a, default)), "g-fraud", out members, out joined);
        Assert.Equal(1, members);
        Assert.False(joined);

        Assert.Equal(404, (await Refused(() => service.JoinAsync(a, "g-nope", true, default))).Status);
    }

    private static void Group(JsonElement state, string id, out int members, out bool joined)
    {
        var group = state.GetProperty("groups").EnumerateArray().Single(item => item.GetProperty("id").GetString() == id);
        members = group.GetProperty("members").GetInt32();
        joined = group.GetProperty("joined").GetBoolean();
    }

    [Fact]
    public async Task LikesToggle_CommentsNotifyTheAuthor_AndOnlyOwnersCanRemove()
    {
        var service = NewService();
        var ann = Person("ann", "Ann");
        var ben = Person("ben", "Ben");
        await Ready(service, ann);
        await Ready(service, ben);
        var id = J(await service.PostAsync(ann, "Proactive calls cut complaints.", "Claims", default)).GetProperty("id").GetString()!;

        var liked = J(await service.ToggleLikeAsync(ben, id, default));
        Assert.True(liked.GetProperty("liked").GetBoolean());
        Assert.Equal(1, liked.GetProperty("likes").GetInt32());
        Assert.Equal(0, J(await service.ToggleLikeAsync(ben, id, default)).GetProperty("likes").GetInt32());

        await service.CommentAsync(ben, id, "Same here.", default);
        var state = J(await service.StateAsync(ann, default));
        Assert.Single(state.GetProperty("posts")[0].GetProperty("comments").EnumerateArray());

        Assert.Equal(403, (await Refused(() => service.DeletePostAsync(ben, id, default))).Status);
        await service.DeletePostAsync(ann, id, default);
        Assert.Empty(J(await service.StateAsync(ann, default)).GetProperty("posts").EnumerateArray());
    }

    [Fact]
    public async Task ThreeReports_HideAPostUntilAnOperatorReviewsIt_AndAnOperatorCanRestoreIt()
    {
        var service = NewService();
        var ann = Person("ann", "Ann");
        await Ready(service, ann);
        var id = J(await service.PostAsync(ann, "A post that people report.", "Claims", default)).GetProperty("id").GetString()!;

        foreach (var reporter in new[] { "r1", "r2" })
        {
            await service.ReportAsync(Person(reporter, reporter), id, "Spam", default);
        }

        Assert.Single(J(await service.StateAsync(ann, default)).GetProperty("posts").EnumerateArray());
        await service.ReportAsync(Person("r3", "r3"), id, null, default);
        Assert.Empty(J(await service.StateAsync(ann, default)).GetProperty("posts").EnumerateArray());

        var reported = J(await service.ReportsAsync(default)).GetProperty("reported").EnumerateArray().ToList();
        Assert.Single(reported);
        Assert.True(reported[0].GetProperty("hidden").GetBoolean());

        await service.SetHiddenAsync(id, false, default);
        Assert.Single(J(await service.StateAsync(ann, default)).GetProperty("posts").EnumerateArray());
        Assert.Empty(J(await service.ReportsAsync(default)).GetProperty("reported").EnumerateArray());
    }

    [Fact]
    public async Task Events_NeedAFutureTime_RsvpToggles_AndDiscordMustBeADiscordLink()
    {
        var service = NewService();
        var ann = Person("ann", "Ann");

        Assert.Equal(400, (await Refused(() => service.CreateEventAsync("Past", DateTimeOffset.UtcNow.AddDays(-2), null, null, default))).Status);
        Assert.Equal(400, (await Refused(() => service.CreateEventAsync("Ok", DateTimeOffset.UtcNow.AddDays(2), null, "http://insecure.example", default))).Status);
        var id = J(await service.CreateEventAsync("Claims CX roundtable", DateTimeOffset.UtcNow.AddDays(3), "Open to all", "https://teams.example/room", default)).GetProperty("id").GetString()!;

        Assert.True(J(await service.ToggleRsvpAsync(ann, id, default)).GetProperty("going").GetBoolean());
        var ev = J(await service.StateAsync(ann, default)).GetProperty("events")[0];
        Assert.Equal(1, ev.GetProperty("going").GetInt32());
        Assert.True(ev.GetProperty("rsvp").GetBoolean());
        Assert.False(J(await service.ToggleRsvpAsync(ann, id, default)).GetProperty("going").GetBoolean());

        Assert.Equal(JsonValueKind.Null, J(await service.StateAsync(ann, default)).GetProperty("discordUrl").ValueKind);
        Assert.Equal(400, (await Refused(() => service.SetDiscordAsync("https://evil.example/invite", default))).Status);
        await service.SetDiscordAsync("https://discord.gg/abc123", default);
        Assert.Equal("https://discord.gg/abc123", J(await service.StateAsync(ann, default)).GetProperty("discordUrl").GetString());
        await service.SetDiscordAsync("", default);
        Assert.Equal(JsonValueKind.Null, J(await service.StateAsync(ann, default)).GetProperty("discordUrl").ValueKind);
    }
}
