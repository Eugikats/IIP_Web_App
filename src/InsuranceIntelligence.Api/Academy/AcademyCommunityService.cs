using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>
/// The Academy's one shared community: people from different insurers posting, replying and joining groups and events.
/// Because names and job titles are shown across organisations, nobody can post or reply until they have said they
/// understand that. Posts are kept out of any insurer's data and never carry customer information (a post that looks like
/// it holds a phone, policy or claim number is refused).
/// </summary>
public sealed partial class AcademyCommunityService(IAcademyRecordStore store, AcademyLearnerService learners, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly string[] Tags = ["Claims", "Question", "Sales", "Fraud"];
    public const int MaxPostLength = 2000;
    public const int MaxCommentLength = 800;
    private const int AutoHideReports = 3;
    private const string Community = "-";

    public static readonly IReadOnlyList<GroupRecord> DefaultGroups =
    [
        new("g-claims", "Claims Professionals", "Claims handling, CX and service recovery", "headset", "#1E4FD8"),
        new("g-fraud", "Fraud & Investigations", "Red flags, live cases and investigation tips", "shield", "#13227A"),
        new("g-ai", "AI in Insurance", "Tools, pilots and what actually works", "sparkle", "#E07A10"),
        new("g-bancassurance", "Bancassurance Network", "Bank partnerships and embedded cover", "bank", "#0B6E6E"),
        new("g-underwriting", "Underwriting & Pricing", "Risk data, pricing and portfolio health", "chart", "#7A2E8C"),
        new("g-health", "Health Insurance", "Schemes, providers and medical claims", "heart", "#047857"),
    ];

    [GeneratedRegex(@"(\d[\s\-]?){9,}")]
    private static partial Regex LongNumber();

    private async Task<T?> ReadAsync<T>(string kind, string owner, string key, CancellationToken ct) where T : class
    {
        var record = await store.GetAsync(kind, owner, key, ct);
        return record is null ? null : JsonSerializer.Deserialize<T>(record.Json, Json);
    }

    private Task WriteAsync<T>(string kind, string owner, string key, T value, CancellationToken ct) =>
        store.SaveAsync(kind, owner, key, JsonSerializer.Serialize(value, Json), ct);

    private static string Owner(string userId) => userId.Trim().ToLowerInvariant();

    // ---------------------------------------------------------------- reading

    public async Task<object> StateAsync(AcademyLearner learner, CancellationToken ct)
    {
        var me = Owner(learner.UserId);
        var profile = await learners.ProfileAsync(learner, ct);
        var groups = await GroupsAsync(ct);
        var memberships = await store.ListByKindAsync(AcademyKinds.Membership, 2000, ct);
        var counts = memberships.GroupBy(row => row.Key).ToDictionary(group => group.Key, group => group.Count());
        var joined = memberships.Where(row => row.Owner == me).Select(row => row.Key).ToHashSet();
        var rsvps = await store.ListByKindAsync(AcademyKinds.Rsvp, 2000, ct);
        var goingCounts = rsvps.GroupBy(row => row.Key).ToDictionary(group => group.Key, group => group.Count());
        var mine = rsvps.Where(row => row.Owner == me).Select(row => row.Key).ToHashSet();

        var events = (await store.ListByKindAsync(AcademyKinds.Event, 200, ct))
            .Select(row => JsonSerializer.Deserialize<EventRecord>(row.Json, Json)!)
            .Where(item => item.StartsAt >= clock.GetUtcNow().AddDays(-1))
            .OrderBy(item => item.StartsAt)
            .Select(item => new
            {
                id = item.Id, title = item.Title, startsAt = item.StartsAt, description = item.Description, link = item.Link,
                going = goingCounts.GetValueOrDefault(item.Id), rsvp = mine.Contains(item.Id),
            })
            .ToList();

        var posts = (await store.ListByKindAsync(AcademyKinds.Post, 300, ct))
            .Select(row => JsonSerializer.Deserialize<PostRecord>(row.Json, Json)!)
            .Where(post => !post.Hidden)
            .OrderByDescending(post => post.CreatedAt)
            .Select(post => new
            {
                id = post.Id,
                author = post.Author,
                role = post.Role,
                tag = post.Tag,
                text = post.Text,
                createdAt = post.CreatedAt,
                likes = post.Likes.Count,
                liked = post.Likes.Contains(me),
                mine = post.UserId == me,
                comments = post.Comments.Select(comment => new { id = comment.Id, author = comment.Author, text = comment.Text, at = comment.At, mine = comment.UserId == me }),
            })
            .ToList();

        var discord = await ReadAsync<SettingRecord>(AcademyKinds.Setting, Community, "discord", ct);
        return new
        {
            consent = profile.CommunityConsent,
            discordUrl = discord?.Value,
            joinedAny = joined.Count > 0,
            groups = groups.Select(group => new { id = group.Id, name = group.Name, description = group.Description, icon = group.Icon, color = group.Color, members = counts.GetValueOrDefault(group.Id), joined = joined.Contains(group.Id) }),
            events,
            posts,
            tags = Tags,
        };
    }

    public async Task<IReadOnlyList<GroupRecord>> GroupsAsync(CancellationToken ct)
    {
        var extra = (await store.ListByKindAsync(AcademyKinds.Group, 100, ct)).Select(row => JsonSerializer.Deserialize<GroupRecord>(row.Json, Json)!);
        return [.. DefaultGroups, .. extra.OrderBy(group => group.Name)];
    }

    // ---------------------------------------------------------------- consent, groups

    public async Task GiveConsentAsync(AcademyLearner learner, CancellationToken ct)
    {
        var profile = await learners.ProfileAsync(learner, ct);
        if (profile.CommunityConsent)
        {
            return;
        }

        await WriteAsync(AcademyKinds.Profile, Owner(learner.UserId), "profile", profile with { CommunityConsent = true, CommunityConsentAt = clock.GetUtcNow() }, ct);
    }

    private async Task<AcademyProfile> ConsentedProfileAsync(AcademyLearner learner, CancellationToken ct)
    {
        var profile = await learners.ProfileAsync(learner, ct);
        return profile.CommunityConsent ? profile : throw new AcademyRuleException(409, "Read and accept the community notice before you post.", "consent");
    }

    public async Task JoinAsync(AcademyLearner learner, string groupId, bool join, CancellationToken ct)
    {
        var group = (await GroupsAsync(ct)).FirstOrDefault(item => item.Id == groupId) ?? throw new AcademyRuleException(404, "That group was not found.");
        if (join)
        {
            await WriteAsync(AcademyKinds.Membership, Owner(learner.UserId), group.Id, new MembershipRecord(group.Id, clock.GetUtcNow()), ct);
        }
        else
        {
            await store.DeleteAsync(AcademyKinds.Membership, Owner(learner.UserId), group.Id, ct);
        }
    }

    private async Task<bool> InAnyGroupAsync(string owner, CancellationToken ct) =>
        (await store.ListByOwnerAsync(AcademyKinds.Membership, owner, ct)).Count > 0;

    // ---------------------------------------------------------------- posts

    private static string CleanText(string? text, int max, string what)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw new AcademyRuleException(400, $"Write your {what} first.");
        }

        if (value.Length > max)
        {
            throw new AcademyRuleException(400, $"A {what} can be up to {max} characters.");
        }

        if (LongNumber().IsMatch(value))
        {
            throw new AcademyRuleException(400, "Remove phone, policy or claim numbers. Do not share customer information in the community.", "personal_data");
        }

        return value;
    }

    private static string RoleLine(AcademyProfile profile) => string.Join(", ", new[] { profile.JobTitle, profile.Organisation }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public async Task<object> PostAsync(AcademyLearner learner, string? text, string? tag, CancellationToken ct)
    {
        var profile = await ConsentedProfileAsync(learner, ct);
        var owner = Owner(learner.UserId);
        if (!await InAnyGroupAsync(owner, ct))
        {
            throw new AcademyRuleException(409, "Join a group to start posting.", "no_group");
        }

        var clean = CleanText(text, MaxPostLength, "post");
        var chosen = Tags.FirstOrDefault(item => string.Equals(item, tag, StringComparison.OrdinalIgnoreCase)) ?? throw new AcademyRuleException(400, "Choose a topic for your post.");
        var post = new PostRecord
        {
            Id = Guid.NewGuid().ToString("N")[..12], UserId = owner, Author = string.IsNullOrWhiteSpace(profile.Name) ? learner.Name : profile.Name,
            Role = RoleLine(profile), Tag = chosen, Text = clean, CreatedAt = clock.GetUtcNow(),
        };
        await WriteAsync(AcademyKinds.Post, Community, post.Id, post, ct);
        return new { id = post.Id };
    }

    private async Task<PostRecord> FindPostAsync(string postId, CancellationToken ct) =>
        await ReadAsync<PostRecord>(AcademyKinds.Post, Community, postId, ct) is { Hidden: false } post ? post : throw new AcademyRuleException(404, "That post was not found.");

    public async Task<object> ToggleLikeAsync(AcademyLearner learner, string postId, CancellationToken ct)
    {
        var post = await FindPostAsync(postId, ct);
        var owner = Owner(learner.UserId);
        var likes = post.Likes.Contains(owner) ? post.Likes.Where(id => id != owner).ToList() : [.. post.Likes, owner];
        await WriteAsync(AcademyKinds.Post, Community, post.Id, post with { Likes = likes }, ct);
        return new { liked = likes.Contains(owner), likes = likes.Count };
    }

    public async Task CommentAsync(AcademyLearner learner, string postId, string? text, CancellationToken ct)
    {
        var profile = await ConsentedProfileAsync(learner, ct);
        var post = await FindPostAsync(postId, ct);
        var clean = CleanText(text, MaxCommentLength, "comment");
        var comment = new CommentRecord(Guid.NewGuid().ToString("N")[..10], Owner(learner.UserId), string.IsNullOrWhiteSpace(profile.Name) ? learner.Name : profile.Name, clean, clock.GetUtcNow());
        await WriteAsync(AcademyKinds.Post, Community, post.Id, post with { Comments = [.. post.Comments, comment] }, ct);
        if (post.UserId != Owner(learner.UserId))
        {
            await NotifyAsync(post.UserId, $"{comment.Author} replied to your post in the community.", "/academy/community", "comment", ct);
        }
    }

    public async Task DeletePostAsync(AcademyLearner learner, string postId, CancellationToken ct)
    {
        var post = await ReadAsync<PostRecord>(AcademyKinds.Post, Community, postId, ct) ?? throw new AcademyRuleException(404, "That post was not found.");
        if (post.UserId != Owner(learner.UserId) && !learner.IsOperator)
        {
            throw new AcademyRuleException(403, "You can only remove your own posts.");
        }

        await store.DeleteAsync(AcademyKinds.Post, Community, postId, ct);
    }

    public async Task ReportAsync(AcademyLearner learner, string postId, string? reason, CancellationToken ct)
    {
        var post = await FindPostAsync(postId, ct);
        var owner = Owner(learner.UserId);
        var report = new ReportRecord(post.Id, owner, (reason ?? string.Empty).Trim() is { Length: > 0 } given ? (given.Length > 300 ? given[..300] : given) : "No reason given", clock.GetUtcNow());
        await WriteAsync(AcademyKinds.Report, post.Id, owner, report, ct);
        var count = (await store.ListByOwnerAsync(AcademyKinds.Report, post.Id, ct)).Count;
        if (count >= AutoHideReports)
        {
            await WriteAsync(AcademyKinds.Post, Community, post.Id, post with { Hidden = true, HiddenReason = "Reported by several people, waiting for review" }, ct);
        }
    }

    // ---------------------------------------------------------------- events

    public async Task<object> ToggleRsvpAsync(AcademyLearner learner, string eventId, CancellationToken ct)
    {
        var item = await ReadAsync<EventRecord>(AcademyKinds.Event, Community, eventId, ct) ?? throw new AcademyRuleException(404, "That event was not found.");
        var owner = Owner(learner.UserId);
        var existing = await store.GetAsync(AcademyKinds.Rsvp, owner, item.Id, ct);
        if (existing is null)
        {
            await WriteAsync(AcademyKinds.Rsvp, owner, item.Id, new { eventId = item.Id, at = clock.GetUtcNow() }, ct);
        }
        else
        {
            await store.DeleteAsync(AcademyKinds.Rsvp, owner, item.Id, ct);
        }

        return new { going = existing is null, title = item.Title };
    }

    // ---------------------------------------------------------------- operators

    public async Task<object> CreateEventAsync(string? title, DateTimeOffset? startsAt, string? description, string? link, CancellationToken ct)
    {
        var name = (title ?? string.Empty).Trim();
        if (name.Length is < 3 or > 120)
        {
            throw new AcademyRuleException(400, "Give the event a title of 3 to 120 characters.");
        }

        if (startsAt is null || startsAt < clock.GetUtcNow().AddMinutes(-5))
        {
            throw new AcademyRuleException(400, "Choose a start time in the future.");
        }

        var safeLink = SafeLink(link);
        var item = new EventRecord(Guid.NewGuid().ToString("N")[..10], name, startsAt.Value, (description ?? string.Empty).Trim() is { Length: > 400 } d ? d[..400] : (description ?? string.Empty).Trim(), safeLink);
        await WriteAsync(AcademyKinds.Event, Community, item.Id, item, ct);
        return new { id = item.Id };
    }

    public Task DeleteEventAsync(string eventId, CancellationToken ct) => store.DeleteAsync(AcademyKinds.Event, Community, eventId, ct);

    public async Task<object> CreateGroupAsync(string? name, string? description, CancellationToken ct)
    {
        var title = (name ?? string.Empty).Trim();
        if (title.Length is < 3 or > 60)
        {
            throw new AcademyRuleException(400, "Give the group a name of 3 to 60 characters.");
        }

        if ((await GroupsAsync(ct)).Any(group => string.Equals(group.Name, title, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AcademyRuleException(409, "A group with that name already exists.");
        }

        var group = new GroupRecord("g-" + Guid.NewGuid().ToString("N")[..8], title, (description ?? string.Empty).Trim() is { Length: > 140 } d ? d[..140] : (description ?? string.Empty).Trim(), "users", "#1E4FD8");
        await WriteAsync(AcademyKinds.Group, Community, group.Id, group, ct);
        return new { id = group.Id };
    }

    public async Task SetDiscordAsync(string? url, CancellationToken ct)
    {
        var value = (url ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            await store.DeleteAsync(AcademyKinds.Setting, Community, "discord", ct);
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("discord.gg", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new AcademyRuleException(400, "Enter a Discord invite link that starts with https://discord.gg/ or https://discord.com/invite/.");
        }

        await WriteAsync(AcademyKinds.Setting, Community, "discord", new SettingRecord(uri.ToString()), ct);
    }

    private static string? SafeLink(string? link)
    {
        var value = (link ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.ToString()
            : throw new AcademyRuleException(400, "An event link must start with https://.");
    }

    public async Task<object> ReportsAsync(CancellationToken ct)
    {
        var posts = (await store.ListByKindAsync(AcademyKinds.Post, 300, ct)).Select(row => JsonSerializer.Deserialize<PostRecord>(row.Json, Json)!).ToList();
        var reports = await store.ListByKindAsync(AcademyKinds.Report, 1000, ct);
        var byPost = reports.Select(row => JsonSerializer.Deserialize<ReportRecord>(row.Json, Json)!).GroupBy(report => report.PostId).ToList();
        var rows = new List<object>();
        foreach (var group in byPost)
        {
            var post = posts.FirstOrDefault(item => item.Id == group.Key);
            if (post is null)
            {
                continue;
            }

            rows.Add(new { id = post.Id, author = post.Author, role = post.Role, text = post.Text, hidden = post.Hidden, reports = group.Select(report => new { reason = report.Reason, at = report.At }) });
        }

        return new { reported = rows };
    }

    public async Task SetHiddenAsync(string postId, bool hidden, CancellationToken ct)
    {
        var post = await ReadAsync<PostRecord>(AcademyKinds.Post, Community, postId, ct) ?? throw new AcademyRuleException(404, "That post was not found.");
        await WriteAsync(AcademyKinds.Post, Community, post.Id, post with { Hidden = hidden, HiddenReason = hidden ? "Hidden by the Academy team" : null }, ct);
        if (!hidden)
        {
            foreach (var report in await store.ListByOwnerAsync(AcademyKinds.Report, post.Id, ct))
            {
                await store.DeleteAsync(AcademyKinds.Report, post.Id, report.Key, ct);
            }
        }
    }

    private async Task NotifyAsync(string owner, string text, string href, string icon, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        await WriteAsync(AcademyKinds.Notification, owner, id, new AcademyNotification(id, owner, text, href, icon, false, clock.GetUtcNow()), ct);
    }

    // ---------------------------------------------------------------- records

    public sealed record GroupRecord(string Id, string Name, string Description, string Icon, string Color);

    public sealed record MembershipRecord(string GroupId, DateTimeOffset At);

    public sealed record EventRecord(string Id, string Title, DateTimeOffset StartsAt, string Description, string? Link);

    public sealed record SettingRecord(string Value);

    public sealed record CommentRecord(string Id, string UserId, string Author, string Text, DateTimeOffset At);

    public sealed record ReportRecord(string PostId, string UserId, string Reason, DateTimeOffset At);

    public sealed record PostRecord
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string Author { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Tag { get; init; } = "Claims";
        public string Text { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public IReadOnlyList<string> Likes { get; init; } = [];
        public IReadOnlyList<CommentRecord> Comments { get; init; } = [];
        public bool Hidden { get; init; }
        public string? HiddenReason { get; init; }
    }
}
