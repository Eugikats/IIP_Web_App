using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;

namespace InsuranceIntelligence.Application.CommandCenter;

public sealed record AnalystAnswer(IReadOnlyList<CommandCenterResponseBlock> Blocks, bool UsedModel, string Summary);

public interface ICommandCenterAnalyst
{
    /// <summary>True when the question asks for counts, trends or breakdowns of workspace records.</summary>
    bool Handles(string question);

    Task<AnalystAnswer> AnswerAsync(string question, TimeSpan utcOffset, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The Command Center's analyst: answers "how many", "how has it changed" and "show me a chart" questions
/// over the workspace's own prospects, claims, customers and conversations. A model, when configured,
/// chooses which counts to run through three tools; every figure, chart point and link in the answer
/// comes from those tool results, never from the model's text. Without a model the same counts run from
/// the question as read by <see cref="CommandCenterAnalytics.Parse"/>. Read only: it changes nothing.
/// </summary>
public sealed class CommandCenterAnalyst(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadReadRepository salesLeads,
    ICaseRepository cases,
    ICustomerRepository customers,
    IChannelReadRepository channels,
    IToolCallingChatProvider? chat = null) : ICommandCenterAnalyst
{
    private const int MaxModelSteps = 5;

    private readonly Dictionary<AnalyticsEntity, IReadOnlyList<AnalyticsRecord>> loaded = new();

    public bool Handles(string question) => CommandCenterAnalytics.Parse(question) is not null;

    public async Task<AnalystAnswer> AnswerAsync(string question, TimeSpan utcOffset, string correlationId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var produced = new List<CommandCenterResponseBlock>();
        var touched = new List<AnalyticsEntity>();

        if (chat is not null)
        {
            try
            {
                var text = await RunModelAsync(question, now, utcOffset, produced, touched, correlationId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(text) && produced.Count > 0)
                {
                    return Assemble(DisplayText.WithoutEmDashes(text.Trim()), produced, touched, usedModel: true);
                }

                // An answer that counted nothing (for example "I can count prospects only") is kept
                // only when it states no figure: a number the tools did not produce is never shown.
                if (!string.IsNullOrWhiteSpace(text) && !text.Any(char.IsDigit))
                {
                    return Assemble(DisplayText.WithoutEmDashes(text.Trim()), produced, touched, usedModel: true);
                }
            }
            catch (PermissionDeniedException)
            {
                throw;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The model is unavailable or misbehaved. The counts below do not need it.
                produced.Clear();
                touched.Clear();
            }
        }

        var parsed = CommandCenterAnalytics.Parse(question)
            ?? new AnalyticsQuestion(AnalyticsEntity.Prospects, "today", false, 7, null);
        var sentences = new List<string>();
        var count = await CountAsync(parsed.Entity, parsed.Period, now, utcOffset, produced, touched, cancellationToken);
        sentences.Add(CommandCenterAnalytics.Describe(count));
        if (parsed.WantsSeries && count.Dated)
        {
            await SeriesAsync(parsed.Entity, parsed.SeriesDays, now, utcOffset, produced, touched, cancellationToken);
        }

        if (parsed.BreakdownBy is not null)
        {
            await BreakdownAsync(parsed.Entity, parsed.BreakdownBy, parsed.Period, now, utcOffset, produced, touched, cancellationToken);
        }

        return Assemble(string.Join(" ", sentences), produced, touched, usedModel: false);
    }

    private static AnalystAnswer Assemble(string text, List<CommandCenterResponseBlock> produced, List<AnalyticsEntity> touched, bool usedModel)
    {
        var blocks = new List<CommandCenterResponseBlock> { new TextBlock(text) };
        blocks.AddRange(produced);
        foreach (var entity in touched.Distinct())
        {
            var (label, href) = CommandCenterAnalytics.Destination(entity);
            blocks.Add(new LinkBlock(label, href));
        }

        return new AnalystAnswer(blocks, usedModel, text);
    }

    private async Task<string?> RunModelAsync(
        string question,
        DateTimeOffset now,
        TimeSpan offset,
        List<CommandCenterResponseBlock> produced,
        List<AnalyticsEntity> touched,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var available = await AvailableEntitiesAsync(cancellationToken);
        var localNow = now.ToOffset(offset);
        var messages = new List<AgentChatMessage>
        {
            AgentChatMessage.System(
                "You are the analyst in the Command Center of IIP, an insurance operations platform. "
                + "You answer questions about this workspace's records by calling the tools, then reply in one to three plain sentences. "
                + "Use only figures returned by the tools. Never estimate, round up or invent a number. "
                + "If the tools cannot answer, say what you can count instead. "
                + "Charts are drawn automatically from daily_series and breakdown results, so do not describe a chart point by point. "
                + "Prospects are people who came to the business as possible customers (acquisition). Customers are existing policyholders. "
                + "You do not make, recommend or imply insurance decisions. Do not use em dashes or emoji. "
                + $"The records you may count: {string.Join(", ", available.Select(e => e.ToString().ToLowerInvariant()))}. "
                + $"Today is {localNow.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture)} in the asker's time zone."),
            AgentChatMessage.User(question),
        };

        var tools = ToolSpecs(available);
        for (var step = 0; step < MaxModelSteps; step++)
        {
            var next = await chat!.NextAsync(messages, tools, correlationId, cancellationToken);
            if (next.ToolCalls.Count == 0)
            {
                return next.Content;
            }

            messages.Add(AgentChatMessage.Assistant(next.Content, next.ToolCalls));
            foreach (var call in next.ToolCalls)
            {
                var result = await RunToolAsync(call, available, now, offset, produced, touched, cancellationToken);
                messages.Add(AgentChatMessage.Tool(call.Id, result));
            }
        }

        return null;
    }

    private async Task<string> RunToolAsync(
        AgentToolCall call,
        IReadOnlyList<AnalyticsEntity> available,
        DateTimeOffset now,
        TimeSpan offset,
        List<CommandCenterResponseBlock> produced,
        List<AnalyticsEntity> touched,
        CancellationToken cancellationToken)
    {
        JsonElement args;
        try
        {
            args = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson).RootElement;
        }
        catch (JsonException)
        {
            return Error("The arguments were not valid JSON.");
        }

        if (!TryEntity(args, out var entity) || !available.Contains(entity))
        {
            return Error($"Unknown or unavailable record type. Use one of: {string.Join(", ", available.Select(e => e.ToString().ToLowerInvariant()))}.");
        }

        switch (call.Name)
        {
            case "count_records":
            {
                var period = Str(args, "period") ?? "all_time";
                var count = await CountAsync(entity, period, now, offset, produced, touched, cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    entity = entity.ToString().ToLowerInvariant(),
                    period = count.Period,
                    period_label = count.PeriodLabel,
                    count = count.Count,
                    previous_count = count.PreviousCount,
                    previous_label = count.PreviousLabel,
                    percent_change = count.PercentChange,
                    dated = count.Dated,
                    sentence = CommandCenterAnalytics.Describe(count),
                });
            }
            case "daily_series":
            {
                var days = args.TryGetProperty("days", out var d) && d.TryGetInt32(out var n) ? n : 7;
                var points = await SeriesAsync(entity, days, now, offset, produced, touched, cancellationToken);
                if (points is null) return Error("These records carry no date, so they cannot be counted by day.");
                return JsonSerializer.Serialize(new { entity = entity.ToString().ToLowerInvariant(), points = points.Select(p => new { day = p.Label, count = p.Value }) });
            }
            case "breakdown":
            {
                var by = Str(args, "by") ?? string.Empty;
                if (!CommandCenterAnalytics.Dimensions[entity].Contains(by))
                {
                    return Error($"{entity} can be broken down by: {string.Join(", ", CommandCenterAnalytics.Dimensions[entity])}.");
                }

                var period = Str(args, "period") ?? "all_time";
                var points = await BreakdownAsync(entity, by, period, now, offset, produced, touched, cancellationToken);
                return JsonSerializer.Serialize(new { entity = entity.ToString().ToLowerInvariant(), by, period, groups = points.Select(p => new { label = p.Label, count = p.Value }) });
            }
            default:
                return Error("Unknown tool.");
        }
    }

    private async Task<AnalyticsCount> CountAsync(
        AnalyticsEntity entity, string period, DateTimeOffset now, TimeSpan offset,
        List<CommandCenterResponseBlock> produced, List<AnalyticsEntity> touched, CancellationToken cancellationToken)
    {
        var records = await LoadAsync(entity, cancellationToken);
        var count = CommandCenterAnalytics.Count(entity, records, period, now, offset);
        touched.Add(entity);

        var noun = CommandCenterAnalytics.Noun(entity, 2);
        var label = count.Period == "all_time" ? $"{Capitalise(noun)} in total" : $"{Capitalise(noun)} {count.PeriodLabel}";
        var pills = new List<MetricPill> { new(label, count.Count.ToString("N0", CultureInfo.InvariantCulture), CommandCenterBlockVocabulary.ToneInfo) };
        if (count.PreviousCount is { } previous && count.PreviousLabel is not null)
        {
            pills.Add(new MetricPill(Capitalise(count.PreviousLabel), previous.ToString("N0", CultureInfo.InvariantCulture), CommandCenterBlockVocabulary.ToneNeutral));
            if (count.PercentChange is { } change)
            {
                pills.Add(new MetricPill(
                    "Change",
                    (change > 0 ? "+" : string.Empty) + change.ToString("0.#", CultureInfo.InvariantCulture) + "%",
                    change > 0 ? CommandCenterBlockVocabulary.ToneSuccess : change < 0 ? CommandCenterBlockVocabulary.ToneWarning : CommandCenterBlockVocabulary.ToneNeutral));
            }
        }

        produced.Add(new MetricPillsBlock(pills));
        return count;
    }

    private async Task<IReadOnlyList<ChartPoint>?> SeriesAsync(
        AnalyticsEntity entity, int days, DateTimeOffset now, TimeSpan offset,
        List<CommandCenterResponseBlock> produced, List<AnalyticsEntity> touched, CancellationToken cancellationToken)
    {
        var records = await LoadAsync(entity, cancellationToken);
        if (records.Count > 0 && records.All(r => r.At is null)) return null;
        var points = CommandCenterAnalytics.DailySeries(records, days, now, offset);
        touched.Add(entity);
        var noun = CommandCenterAnalytics.Noun(entity, 2);
        var total = points.Sum(p => p.Value);
        produced.Add(new ChartBlock(
            $"{Capitalise(noun)} per day, last {points.Count} days",
            "line",
            points,
            noun,
            $"{total.ToString("N0", CultureInfo.InvariantCulture)} in all over these {points.Count} days."));
        return points;
    }

    private async Task<IReadOnlyList<ChartPoint>> BreakdownAsync(
        AnalyticsEntity entity, string by, string period, DateTimeOffset now, TimeSpan offset,
        List<CommandCenterResponseBlock> produced, List<AnalyticsEntity> touched, CancellationToken cancellationToken)
    {
        var records = await LoadAsync(entity, cancellationToken);
        var points = CommandCenterAnalytics.Breakdown(records, by, period, now, offset);
        touched.Add(entity);
        var (_, _, label, _, _) = CommandCenterAnalytics.Resolve(CommandCenterAnalytics.Periods.Contains(period) ? period : "all_time", now, offset);
        var noun = CommandCenterAnalytics.Noun(entity, 2);
        produced.Add(new ChartBlock($"{Capitalise(noun)} by {by}, {label}", "bar", points, noun));
        return points;
    }

    private async Task<IReadOnlyList<AnalyticsEntity>> AvailableEntitiesAsync(CancellationToken cancellationToken)
    {
        var available = new List<AnalyticsEntity>();
        if (await permissionService.HasPermissionAsync(Permission.ViewSalesLeads, cancellationToken)) available.Add(AnalyticsEntity.Prospects);
        if (await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken)) available.Add(AnalyticsEntity.Claims);
        if (await permissionService.HasPermissionAsync(Permission.ViewCustomers, cancellationToken)) available.Add(AnalyticsEntity.Customers);
        if (await permissionService.HasPermissionAsync(Permission.ViewChannels, cancellationToken)) available.Add(AnalyticsEntity.Conversations);
        return available;
    }

    private async Task<IReadOnlyList<AnalyticsRecord>> LoadAsync(AnalyticsEntity entity, CancellationToken cancellationToken)
    {
        if (loaded.TryGetValue(entity, out var cached)) return cached;

        await permissionService.RequirePermissionAsync(entity switch
        {
            AnalyticsEntity.Prospects => Permission.ViewSalesLeads,
            AnalyticsEntity.Claims => Permission.ViewAllClaims,
            AnalyticsEntity.Customers => Permission.ViewCustomers,
            _ => Permission.ViewChannels,
        }, cancellationToken);

        var context = tenantContextAccessor.Current;
        IReadOnlyList<AnalyticsRecord> records = entity switch
        {
            AnalyticsEntity.Prospects => (await salesLeads.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken))
                .Select(lead => new AnalyticsRecord(lead.CreatedAt, new Dictionary<string, string>
                {
                    ["source"] = Humanise(lead.LeadSource),
                    ["stage"] = Humanise(lead.QuoteStatus),
                    ["product"] = Humanise(lead.AppliedFor ?? lead.ProductFit),
                    ["channel"] = Humanise(lead.PrimaryChannel),
                }))
                .ToList(),
            AnalyticsEntity.Claims => (await cases.ListAsync(context.Scope, cancellationToken))
                .Select(claim => new AnalyticsRecord(claim.CreatedAt, new Dictionary<string, string>
                {
                    ["status"] = ClaimStatusLabels.For(claim.Status),
                    ["product"] = Humanise(claim.ProductLine),
                }))
                .ToList(),
            AnalyticsEntity.Customers => (await customers.ListAsync(context.Scope, cancellationToken))
                .Select(_ => new AnalyticsRecord(null, new Dictionary<string, string>()))
                .ToList(),
            _ => (await channels.GetOverviewAsync(context.TenantId, context.WorkspaceId, cancellationToken))
                .Select(conversation => new AnalyticsRecord(conversation.LastActivityAt, new Dictionary<string, string>
                {
                    ["channel"] = Humanise(conversation.Channel),
                    ["status"] = Humanise(conversation.Status),
                }))
                .ToList(),
        };

        loaded[entity] = records;
        return records;
    }

    private static IReadOnlyList<AgentToolSpec> ToolSpecs(IReadOnlyList<AnalyticsEntity> available)
    {
        var entityEnum = available.Select(e => e.ToString().ToLowerInvariant()).ToArray();
        var periods = CommandCenterAnalytics.Periods.ToArray();
        return
        [
            new AgentToolSpec(
                "count_records",
                "Counts records in a period and compares with the period of equal length before it. Customers carry no date and are counted in total only.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        entity = new { type = "string", @enum = entityEnum },
                        period = new { type = "string", @enum = periods },
                    },
                    required = new[] { "entity", "period" },
                }),
            new AgentToolSpec(
                "daily_series",
                "Counts records per day for the last N days (2 to 90), today included. Draws a line chart.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        entity = new { type = "string", @enum = entityEnum },
                        days = new { type = "integer", minimum = 2, maximum = 90 },
                    },
                    required = new[] { "entity", "days" },
                }),
            new AgentToolSpec(
                "breakdown",
                "Groups records in a period by a field. Prospects: source, stage, product, channel. Claims: status, product. Conversations: channel, status. Draws a bar chart.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        entity = new { type = "string", @enum = entityEnum },
                        by = new { type = "string", @enum = new[] { "source", "stage", "product", "channel", "status" } },
                        period = new { type = "string", @enum = periods },
                    },
                    required = new[] { "entity", "by" },
                }),
        ];
    }

    private static bool TryEntity(JsonElement args, out AnalyticsEntity entity)
    {
        entity = default;
        var raw = Str(args, "entity");
        return raw is not null && Enum.TryParse(raw, ignoreCase: true, out entity);
    }

    private static string? Str(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Error(string message) => JsonSerializer.Serialize(new { error = message });

    private static string Capitalise(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>"NotStarted" or "not_started" to "Not started", so a stored code never reaches the screen.</summary>
    public static string Humanise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Not recorded";
        value = value.Trim();
        if (value.Equals("whatsapp", StringComparison.OrdinalIgnoreCase)) return "WhatsApp";
        if (value.Equals("sms", StringComparison.OrdinalIgnoreCase)) return "SMS";

        // Already written for people (a product name such as "Motor Comprehensive"): leave it alone.
        if (value.Contains(' ')) return value;
        var spaced = Regex.Replace(value.Trim().Replace('_', ' ').Replace('-', ' '), "(?<=[a-z])(?=[A-Z])", " ");
        spaced = Regex.Replace(spaced, @"\s+", " ");
        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }
}
