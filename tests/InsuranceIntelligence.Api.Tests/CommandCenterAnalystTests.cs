using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using DomainCustomer = InsuranceIntelligence.Domain.Customers.Customer;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CommandCenterAnalystTests
{
    private static readonly TimeSpan Kampala = TimeSpan.FromHours(3);

    // Friday 9 October 2026, 10:00 in Kampala.
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 7, 0, 0, TimeSpan.Zero);

    private static AnalyticsRecord At(DateTimeOffset at, string? source = null) =>
        new(at, new Dictionary<string, string> { ["source"] = source ?? "WhatsApp" });

    [Theory]
    [InlineData("How many customers were acquired today?", AnalyticsEntity.Prospects, "today", false)]
    [InlineData("Show me a chart of the % increase in leads over the past week", AnalyticsEntity.Prospects, "last_7_days", true)]
    [InlineData("How many claims this month?", AnalyticsEntity.Claims, "this_month", false)]
    [InlineData("How many customers do we have?", AnalyticsEntity.Customers, "all_time", false)]
    [InlineData("Count WhatsApp conversations yesterday", AnalyticsEntity.Conversations, "yesterday", false)]
    public void Reads_what_a_counting_question_asks(string question, AnalyticsEntity entity, string period, bool series)
    {
        var parsed = CommandCenterAnalytics.Parse(question);

        Assert.NotNull(parsed);
        Assert.Equal(entity, parsed!.Entity);
        Assert.Equal(period, parsed.Period);
        Assert.Equal(series, parsed.WantsSeries);
    }

    [Theory]
    [InlineData("What needs my attention today?")]
    [InlineData("What does the policy say about flood damage?")]
    [InlineData("Draft an update for the customer on claim 12")]
    public void Leaves_other_questions_to_the_rest_of_the_command_center(string question)
    {
        Assert.Null(CommandCenterAnalytics.Parse(question));
    }

    [Fact]
    public void Today_is_the_askers_calendar_day_and_is_compared_with_yesterday()
    {
        var records = new[]
        {
            At(new DateTimeOffset(2026, 10, 9, 0, 30, 0, Kampala)),   // today, just after midnight in Kampala (still 8 Oct in UTC)
            At(new DateTimeOffset(2026, 10, 9, 9, 0, 0, Kampala)),
            At(new DateTimeOffset(2026, 10, 9, 9, 30, 0, Kampala)),
            At(new DateTimeOffset(2026, 10, 8, 12, 0, 0, Kampala)),   // yesterday
            At(new DateTimeOffset(2026, 10, 8, 23, 59, 0, Kampala)),
        };

        var count = CommandCenterAnalytics.Count(AnalyticsEntity.Prospects, records, "today", Now, Kampala);

        Assert.Equal(3, count.Count);
        Assert.Equal(2, count.PreviousCount);
        Assert.Equal(50m, count.PercentChange);
        Assert.Equal("3 prospects came in today, up 50% on yesterday (2).", CommandCenterAnalytics.Describe(count));
    }

    [Fact]
    public void No_previous_records_gives_no_percentage_rather_than_infinity()
    {
        var count = CommandCenterAnalytics.Count(AnalyticsEntity.Claims, [At(Now)], "today", Now, Kampala);

        Assert.Equal(1, count.Count);
        Assert.Equal(0, count.PreviousCount);
        Assert.Null(count.PercentChange);
    }

    [Fact]
    public void A_daily_series_has_one_point_per_day_ending_today()
    {
        var records = new[] { At(Now), At(Now), At(Now.AddDays(-2)) };

        var points = CommandCenterAnalytics.DailySeries(records, 7, Now, Kampala);

        Assert.Equal(7, points.Count);
        Assert.Equal("9 Oct", points[^1].Label);
        Assert.Equal(2, points[^1].Value);
        Assert.Equal(1, points[^3].Value);
        Assert.Equal(3, points.Sum(point => point.Value));
    }

    [Fact]
    public void Customers_without_dates_are_counted_in_total_and_say_why()
    {
        var count = CommandCenterAnalytics.Count(
            AnalyticsEntity.Customers,
            [new AnalyticsRecord(null, new Dictionary<string, string>()), new AnalyticsRecord(null, new Dictionary<string, string>())],
            "today", Now, Kampala);

        Assert.False(count.Dated);
        Assert.Equal(2, count.Count);
        Assert.Contains("cannot be counted by day", CommandCenterAnalytics.Describe(count));
    }

    [Fact]
    public async Task Without_a_model_the_answer_is_counted_from_the_question_with_a_chart_and_a_link()
    {
        var leads = new[] { Lead("L1", Now), Lead("L2", Now.AddDays(-1)), Lead("L3", Now.AddDays(-3)) };
        var analyst = Analyst(leads);

        var answer = await analyst.AnswerAsync("Chart the increase in leads over the past week", Kampala, "c-1");

        Assert.False(answer.UsedModel);
        Assert.IsType<TextBlock>(answer.Blocks[0]);
        var chart = Assert.Single(answer.Blocks.OfType<ChartBlock>());
        Assert.Equal("line", chart.Kind);
        Assert.Equal(3, chart.Points.Sum(point => point.Value));
        var link = Assert.Single(answer.Blocks.OfType<LinkBlock>());
        Assert.Equal("/app/acquisition", link.Href);
    }

    [Fact]
    public async Task With_a_model_figures_come_from_tool_results_and_the_text_is_cleaned()
    {
        var chat = new ScriptedChat(
            new AgentChatStep(null, [new AgentToolCall("t1", "count_records", """{"entity":"prospects","period":"today"}""")]),
            new AgentChatStep("Two prospects came in today — both on WhatsApp.", []));
        var analyst = Analyst([Lead("L1", Now), Lead("L2", Now)], chat);

        var answer = await analyst.AnswerAsync("How many customers were acquired today?", Kampala, "c-2");

        Assert.True(answer.UsedModel);
        Assert.DoesNotContain("—", ((TextBlock)answer.Blocks[0]).Markdown);
        var pills = Assert.Single(answer.Blocks.OfType<MetricPillsBlock>());
        Assert.Equal("2", pills.Items[0].Value);
        Assert.Contains(answer.Blocks.OfType<LinkBlock>(), link => link.Href == "/app/acquisition");
    }

    [Fact]
    public async Task A_failing_model_falls_back_to_the_counts()
    {
        var analyst = Analyst([Lead("L1", Now)], new ScriptedChat(throwOnCall: true));

        var answer = await analyst.AnswerAsync("How many prospects today?", Kampala, "c-3");

        Assert.False(answer.UsedModel);
        Assert.StartsWith("1 prospect came in today", ((TextBlock)answer.Blocks[0]).Markdown);
    }

    [Fact]
    public async Task The_model_is_not_offered_records_the_person_may_not_see()
    {
        var chat = new ScriptedChat(new AgentChatStep("I can count prospects only.", []));
        var analyst = Analyst([Lead("L1", Now)], chat, permissions: new Only(Permission.ViewSalesLeads));

        await analyst.AnswerAsync("How many claims today?", Kampala, "c-4");

        var offered = chat.OfferedEntities;
        Assert.Equal(["prospects"], offered);
    }

    [Fact]
    public async Task Counting_a_record_type_without_permission_is_refused()
    {
        var analyst = Analyst([], permissions: new Only(Permission.ViewSalesLeads));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => analyst.AnswerAsync("How many claims today?", Kampala, "c-5"));
    }

    [Theory]
    [InlineData("NotStarted", "Not started")]
    [InlineData("whatsapp", "WhatsApp")]
    [InlineData("Motor Comprehensive", "Motor Comprehensive")]
    [InlineData("", "Not recorded")]
    public void Stored_codes_are_written_for_people(string raw, string expected)
    {
        Assert.Equal(expected, CommandCenterAnalyst.Humanise(raw));
    }

    private static SalesLeadDetailReadModel Lead(string id, DateTimeOffset createdAt) =>
        new(id, "WhatsApp", "need", "summary", "Motor", "NotStarted", [], [], null, null, $"Person {id}", null, createdAt);

    private static CommandCenterAnalyst Analyst(
        IReadOnlyList<SalesLeadDetailReadModel> leads,
        IToolCallingChatProvider? chat = null,
        IPermissionService? permissions = null) =>
        new(new FixedContext(), permissions ?? new Only(Permission.ViewSalesLeads, Permission.ViewAllClaims, Permission.ViewCustomers, Permission.ViewChannels),
            new Leads(leads), new NoCases(), new NoCustomers(), new NoChannels(), chat);

    private sealed class ScriptedChat(params AgentChatStep[] steps) : IToolCallingChatProvider
    {
        private readonly Queue<AgentChatStep> queue = new(steps);
        private readonly bool fail;

        public ScriptedChat(bool throwOnCall) : this() => fail = throwOnCall;

        public IReadOnlyList<string> OfferedEntities { get; private set; } = [];

        public Task<AgentChatStep> NextAsync(IReadOnlyList<AgentChatMessage> messages, IReadOnlyList<AgentToolSpec> tools, string? correlationId, CancellationToken cancellationToken = default)
        {
            if (fail) throw new HttpRequestException("model down");
            var json = System.Text.Json.JsonSerializer.Serialize(tools[0].Parameters);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            OfferedEntities = doc.RootElement.GetProperty("properties").GetProperty("entity").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToList();
            return Task.FromResult(queue.Dequeue());
        }
    }

    private sealed class FixedContext : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-operations", "user-1", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class Only(params Permission[] granted) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(granted.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            granted.Contains(permission) ? Task.CompletedTask : throw new PermissionDeniedException(permission);
    }

    private sealed class Leads(IReadOnlyList<SalesLeadDetailReadModel> leads) : ISalesLeadReadRepository
    {
        public Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default) =>
            Task.FromResult(leads.FirstOrDefault(lead => lead.LeadId == leadId));

        public Task<IReadOnlyList<SalesLeadDetailReadModel>> ListLeadsAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(leads);
    }

    private sealed class NoCases : ICaseRepository
    {
        public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default) => Task.FromResult<ClaimCase?>(null);
        public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ClaimCase>>([]);
    }

    private sealed class NoCustomers : ICustomerRepository
    {
        public Task<DomainCustomer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) => Task.FromResult<DomainCustomer?>(null);
        public Task<IReadOnlyCollection<DomainCustomer>> ListAsync(TenantScope scope, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<DomainCustomer>>([]);
        public Task UpsertAsync(DomainCustomer customer, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoChannels : IChannelReadRepository
    {
        public Task<IReadOnlyList<ChannelConversationReadModel>> GetOverviewAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ChannelConversationReadModel>>([]);
    }
}
