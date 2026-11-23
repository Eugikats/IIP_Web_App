using System.Globalization;
using InsuranceIntelligence.Application.Brief;

namespace InsuranceIntelligence.Application.Voice;

/// <summary>
/// When a spoken nudge may be offered. A nudge is a nudge, not a feed: a few a day, in working hours, and never the
/// same thing twice in a row.
/// </summary>
public sealed class VoiceNudgeOptions
{
    public int MaxPerDay { get; init; } = 3;
    public int WorkStartHour { get; init; } = 8;
    public int WorkEndHour { get; init; } = 18;
    /// <summary>The same thing is not offered again for this many days.</summary>
    public int RepeatDays { get; init; } = 3;
    public string TimeZoneId { get; init; } = "Africa/Kampala";
}

/// <summary>One thing worth saying. <see cref="PrivateText"/> has no names and no figures, for a locked phone.</summary>
public sealed record NudgeCandidate(string Key, string Text, string PrivateText, string? Href);

public sealed record Nudge(string Id, string Text, string? Href, bool Private);

public interface IVoiceNudgeLedger
{
    /// <summary>
    /// Picks the first candidate that may be offered right now and records it, all in one step, so two requests at the
    /// same moment make one nudge between them. Null when it is outside working hours, today's allowance is used, or
    /// everything has been offered recently.
    /// </summary>
    NudgeCandidate? TryClaim(string person, IReadOnlyList<NudgeCandidate> candidates);
}

/// <summary>
/// Kept in memory per API instance, like the voice limits: with more than one instance each counts for itself, so the
/// real daily ceiling is the allowance times the instance count. Restarting the API forgets what was offered.
/// </summary>
public sealed class VoiceNudgeLedger(VoiceNudgeOptions options, TimeProvider? clock = null) : IVoiceNudgeLedger
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object gate = new();
    private readonly Dictionary<string, List<(string Key, DateTimeOffset At)>> offered = new(StringComparer.Ordinal);

    public NudgeCandidate? TryClaim(string person, IReadOnlyList<NudgeCandidate> candidates)
    {
        var now = time.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(now, ResolveZone());
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            || local.Hour < options.WorkStartHour
            || local.Hour >= options.WorkEndHour)
        {
            return null;
        }

        lock (gate)
        {
            if (!offered.TryGetValue(person, out var history))
            {
                history = [];
                offered[person] = history;
            }

            history.RemoveAll(entry => now - entry.At > TimeSpan.FromDays(Math.Max(options.RepeatDays, 1)));
            var today = history.Count(entry => TimeZoneInfo.ConvertTime(entry.At, ResolveZone()).Date == local.Date);
            if (today >= options.MaxPerDay)
            {
                return null;
            }

            var next = candidates.FirstOrDefault(candidate => history.All(entry => !string.Equals(entry.Key, candidate.Key, StringComparison.Ordinal)));
            if (next is null)
            {
                return null;
            }

            history.Add((next.Key, now));
            return next;
        }
    }

    private TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // East Africa Time has no daylight saving, so a fixed offset is the same answer.
            return TimeZoneInfo.CreateCustomTimeZone("EAT", TimeSpan.FromHours(3), "East Africa Time", "East Africa Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("EAT", TimeSpan.FromHours(3), "East Africa Time", "East Africa Time");
        }
    }
}

public interface IVoiceNudges
{
    /// <summary>The next thing worth saying to this person, or null. <paramref name="locked"/> gives the private wording.</summary>
    Task<Nudge?> NextAsync(string person, bool locked, CancellationToken cancellationToken);
}

/// <summary>
/// Nudges come from the same brief the person sees on opening the app, so nothing is said that the brief would not
/// show. They are never read from the model: each line is built from the brief's own fields.
/// </summary>
public sealed class VoiceNudges(IBriefService brief, IVoiceNudgeLedger ledger) : IVoiceNudges
{
    private const string Private = "Something in IIP needs your attention. Open the app when you can.";

    public async Task<Nudge?> NextAsync(string person, bool locked, CancellationToken cancellationToken)
    {
        var today = await brief.GetAsync(cancellationToken);
        var candidates = new List<NudgeCandidate>();
        foreach (var item in today.Items.Take(3))
        {
            candidates.Add(new NudgeCandidate(
                $"claim:{item.CaseId}",
                $"{item.CustomerName}, {item.ClaimType} claim. {item.Reason}",
                Private,
                SafeHref(item.PrimaryActionHref)));
        }

        if (today.Retention is { PoliciesAtRisk: > 0 } retention)
        {
            var policies = retention.PoliciesAtRisk == 1 ? "1 policy is" : $"{retention.PoliciesAtRisk.ToString(CultureInfo.InvariantCulture)} policies are";
            candidates.Add(new NudgeCandidate(
                "retention",
                $"{policies} at risk of lapsing, with {retention.PremiumAtRiskCurrency} {retention.PremiumAtRisk.ToString("N0", CultureInfo.InvariantCulture)} of premium.",
                Private,
                "/app/retention"));
        }

        var claimed = ledger.TryClaim(person, candidates);
        return claimed is null
            ? null
            : new Nudge(Guid.NewGuid().ToString("N"), locked ? claimed.PrivateText : claimed.Text, locked ? null : claimed.Href, locked);
    }

    /// <summary>Only a screen inside IIP is ever offered.</summary>
    private static string? SafeHref(string? href) =>
        href is not null && href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal) && !href.Contains('\\') ? href : null;
}
