using System.Globalization;
using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Application.CommandCenter;

/// <summary>What the Command Center can count.</summary>
public enum AnalyticsEntity
{
    Prospects,
    Claims,
    Customers,
    Conversations,
}

/// <summary>One counted record: when it happened (null when the record carries no date) and the
/// values it can be grouped by.</summary>
public sealed record AnalyticsRecord(DateTimeOffset? At, IReadOnlyDictionary<string, string> Dimensions);

public sealed record AnalyticsCount(
    AnalyticsEntity Entity,
    string Period,
    string PeriodLabel,
    int Count,
    int? PreviousCount,
    string? PreviousLabel,
    decimal? PercentChange,
    bool Dated);

/// <summary>A question already read for what it asks: which records, over which period, and whether a
/// trend or a breakdown was asked for. Used when no language model is available, and to decide
/// whether a question belongs to the analyst at all.</summary>
public sealed record AnalyticsQuestion(AnalyticsEntity Entity, string Period, bool WantsSeries, int SeriesDays, string? BreakdownBy);

/// <summary>
/// Counting over workspace records for the Command Center. Pure functions over already-loaded,
/// already-scoped records, so every figure the analyst shows can be reproduced from the same data.
/// Days are calendar days in the asking person's time zone.
/// </summary>
public static class CommandCenterAnalytics
{
    public static readonly IReadOnlyList<string> Periods =
        ["today", "yesterday", "this_week", "last_7_days", "last_30_days", "this_month", "last_month", "all_time"];

    public static readonly IReadOnlyDictionary<AnalyticsEntity, IReadOnlyList<string>> Dimensions =
        new Dictionary<AnalyticsEntity, IReadOnlyList<string>>
        {
            [AnalyticsEntity.Prospects] = ["source", "stage", "product", "channel"],
            [AnalyticsEntity.Claims] = ["status", "product"],
            [AnalyticsEntity.Customers] = [],
            [AnalyticsEntity.Conversations] = ["channel", "status"],
        };

    public static string Noun(AnalyticsEntity entity, int count) => (entity, count == 1) switch
    {
        (AnalyticsEntity.Prospects, true) => "prospect",
        (AnalyticsEntity.Prospects, false) => "prospects",
        (AnalyticsEntity.Claims, true) => "claim",
        (AnalyticsEntity.Claims, false) => "claims",
        (AnalyticsEntity.Customers, true) => "customer",
        (AnalyticsEntity.Customers, false) => "customers",
        (AnalyticsEntity.Conversations, true) => "conversation",
        _ => "conversations",
    };

    public static (string Label, string Href) Destination(AnalyticsEntity entity) => entity switch
    {
        AnalyticsEntity.Prospects => ("Open the acquisition dashboard", "/app/acquisition"),
        AnalyticsEntity.Claims => ("Open claims", "/app/claims"),
        AnalyticsEntity.Customers => ("Open customers", "/app/customers"),
        _ => ("Open the inbox", "/app/channels"),
    };

    /// <summary>The start (inclusive) and end (exclusive) of a period, in UTC, with a label, and the
    /// period of equal length just before it for comparison. All time has no comparison.</summary>
    public static (DateTimeOffset? Start, DateTimeOffset End, string Label, DateTimeOffset? PrevStart, string? PrevLabel) Resolve(
        string period, DateTimeOffset now, TimeSpan offset)
    {
        var local = now.ToOffset(offset);
        var todayStart = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, offset);
        switch (period)
        {
            case "today":
                return (todayStart, todayStart.AddDays(1), "today", todayStart.AddDays(-1), "yesterday");
            case "yesterday":
                return (todayStart.AddDays(-1), todayStart, "yesterday", todayStart.AddDays(-2), "the day before");
            case "this_week":
            {
                var daysSinceMonday = ((int)local.DayOfWeek + 6) % 7;
                var weekStart = todayStart.AddDays(-daysSinceMonday);
                var elapsed = todayStart.AddDays(1) - weekStart;
                return (weekStart, todayStart.AddDays(1), "this week", weekStart - elapsed, "the same days last week");
            }
            case "last_7_days":
                return (todayStart.AddDays(-6), todayStart.AddDays(1), "the last 7 days", todayStart.AddDays(-13), "the 7 days before");
            case "last_30_days":
                return (todayStart.AddDays(-29), todayStart.AddDays(1), "the last 30 days", todayStart.AddDays(-59), "the 30 days before");
            case "this_month":
            {
                var monthStart = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, offset);
                var elapsedDays = (todayStart - monthStart).Days + 1;
                return (monthStart, todayStart.AddDays(1), "this month", monthStart.AddMonths(-1), $"the first {elapsedDays} days of last month");
            }
            case "last_month":
            {
                var monthStart = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, offset);
                return (monthStart.AddMonths(-1), monthStart, "last month", monthStart.AddMonths(-2), "the month before");
            }
            default:
                return (null, todayStart.AddDays(1), "in total", null, null);
        }
    }

    public static AnalyticsCount Count(
        AnalyticsEntity entity, IReadOnlyList<AnalyticsRecord> records, string period, DateTimeOffset now, TimeSpan offset)
    {
        period = Periods.Contains(period) ? period : "all_time";
        var dated = records.Count == 0 || records.Any(r => r.At is not null);
        var (start, end, label, prevStart, prevLabel) = Resolve(period, now, offset);

        if (start is null || !dated)
        {
            return new AnalyticsCount(entity, "all_time", "in total", records.Count, null, null, null, dated);
        }

        var count = records.Count(r => r.At >= start && r.At < end);
        var previousEnd = period switch
        {
            // The same number of days into last month, so a month in progress is compared fairly.
            "this_month" => prevStart!.Value.AddDays((end - start.Value).Days),
            "last_month" => start.Value,
            _ => prevStart!.Value + (end - start.Value),
        };

        var previous = records.Count(r => r.At >= prevStart && r.At < previousEnd);
        decimal? change = previous == 0 ? null : Math.Round((count - previous) * 100m / previous, 1);
        return new AnalyticsCount(entity, period, label, count, previous, prevLabel, change, dated);
    }

    /// <summary>Counts per calendar day for the last <paramref name="days"/> days, today included.</summary>
    public static IReadOnlyList<ChartPoint> DailySeries(
        IReadOnlyList<AnalyticsRecord> records, int days, DateTimeOffset now, TimeSpan offset)
    {
        days = Math.Clamp(days, 2, 90);
        var local = now.ToOffset(offset);
        var todayStart = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, offset);
        var points = new List<ChartPoint>(days);
        for (var i = days - 1; i >= 0; i--)
        {
            var dayStart = todayStart.AddDays(-i);
            var dayEnd = dayStart.AddDays(1);
            var count = records.Count(r => r.At >= dayStart && r.At < dayEnd);
            points.Add(new ChartPoint(dayStart.ToString("d MMM", CultureInfo.InvariantCulture), count));
        }

        return points;
    }

    public static IReadOnlyList<ChartPoint> Breakdown(
        IReadOnlyList<AnalyticsRecord> records, string dimension, string period, DateTimeOffset now, TimeSpan offset)
    {
        var (start, end, _, _, _) = Resolve(Periods.Contains(period) ? period : "all_time", now, offset);
        return records
            .Where(r => start is null || (r.At >= start && r.At < end))
            .GroupBy(r => r.Dimensions.TryGetValue(dimension, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "Not recorded")
            .Select(g => new ChartPoint(g.Key, g.Count()))
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Label, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    /// <summary>One plain sentence for a count, used as the answer when no model is available and as
    /// the figure the model is given to narrate.</summary>
    public static string Describe(AnalyticsCount count)
    {
        var noun = Noun(count.Entity, count.Count);
        var verb = count.Entity switch
        {
            AnalyticsEntity.Prospects => "came in",
            AnalyticsEntity.Claims => "were opened",
            AnalyticsEntity.Conversations => "had activity",
            _ => "are on record",
        };
        if (count.Entity == AnalyticsEntity.Prospects && count.Count == 1) verb = "came in";
        if (count.Entity == AnalyticsEntity.Claims && count.Count == 1) verb = "was opened";

        if (!count.Dated)
        {
            return count.Entity == AnalyticsEntity.Customers
                ? $"{count.Count.ToString("N0", CultureInfo.InvariantCulture)} {noun} are on record. Customer records carry no joining date, so they cannot be counted by day."
                : $"{count.Count.ToString("N0", CultureInfo.InvariantCulture)} {noun} in total.";
        }

        if (count.Period == "all_time")
        {
            return $"{count.Count.ToString("N0", CultureInfo.InvariantCulture)} {noun} in total.";
        }

        var sentence = $"{count.Count.ToString("N0", CultureInfo.InvariantCulture)} {noun} {verb} {count.PeriodLabel}";
        if (count.PreviousCount is { } previous && count.PreviousLabel is not null)
        {
            sentence += count.PercentChange switch
            {
                null => $", against {previous.ToString("N0", CultureInfo.InvariantCulture)} {count.PreviousLabel}",
                > 0 => $", up {count.PercentChange.Value.ToString("0.#", CultureInfo.InvariantCulture)}% on {count.PreviousLabel} ({previous.ToString("N0", CultureInfo.InvariantCulture)})",
                < 0 => $", down {Math.Abs(count.PercentChange.Value).ToString("0.#", CultureInfo.InvariantCulture)}% on {count.PreviousLabel} ({previous.ToString("N0", CultureInfo.InvariantCulture)})",
                _ => $", the same as {count.PreviousLabel}",
            };
        }

        return sentence + ".";
    }

    private static readonly Regex AnalyticSignal = new(
        @"\b(how many|how much|number of|count|total|increase|decrease|grew|growth|trend|chart|graph|plot|compare|comparison|percent|percentage|breakdown|broken down|by status|by source|by product|by stage|by channel|per day|daily|weekly|so far|statistics|stats)\b|%",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Reads what a question asks for, or returns null when it is not a counting question.
    /// The model, when present, is still free to call the tools differently.</summary>
    public static AnalyticsQuestion? Parse(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || !AnalyticSignal.IsMatch(question)) return null;
        var q = question.ToLowerInvariant();

        AnalyticsEntity? entity =
            Regex.IsMatch(q, @"\b(acquir\w*|acquisition|prospects?|leads?|sign[- ]?ups?|signed up|new customers?)\b") ? AnalyticsEntity.Prospects
            : Regex.IsMatch(q, @"\bclaims?\b") ? AnalyticsEntity.Claims
            : Regex.IsMatch(q, @"\b(conversations?|chats?|messages?|whatsapp|inbox)\b") ? AnalyticsEntity.Conversations
            : Regex.IsMatch(q, @"\bcustomers?\b") ? AnalyticsEntity.Customers
            : null;
        if (entity is null) return null;

        var period =
            q.Contains("yesterday") ? "yesterday"
            : q.Contains("today") ? "today"
            : Regex.IsMatch(q, @"this week") ? "this_week"
            : Regex.IsMatch(q, @"(past|last) (week|7 days|seven days)") ? "last_7_days"
            : Regex.IsMatch(q, @"(past|last) (30 days|thirty days)") ? "last_30_days"
            : q.Contains("this month") ? "this_month"
            : q.Contains("last month") ? "last_month"
            : "all_time";

        var wantsSeries = Regex.IsMatch(q, @"\b(trend|chart|graph|plot|per day|daily|increase|decrease|growth|grew|over the)\b|%|percent");
        var seriesDays = period switch
        {
            "last_30_days" or "this_month" or "last_month" => 30,
            "today" or "yesterday" or "this_week" or "last_7_days" => 7,
            _ => 14,
        };
        if (wantsSeries && period == "all_time") period = "last_7_days";

        string? by = null;
        var byMatch = Regex.Match(q, @"\bby (status|source|product|stage|channel)\b");
        if (byMatch.Success && Dimensions[entity.Value].Contains(byMatch.Groups[1].Value)) by = byMatch.Groups[1].Value;

        return new AnalyticsQuestion(entity.Value, period, wantsSeries, seriesDays, by);
    }
}
