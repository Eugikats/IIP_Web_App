using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Domain.Usage;
using InsuranceIntelligence.Infrastructure.Billing;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class AiCreditsTests : IDisposable
{
    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"credits-{Guid.NewGuid():N}").Options);
    private readonly OrganisationService organisationService;
    private readonly EfOrganisationRepository organisationRepository;
    private readonly EfUsageMeter meter;
    private readonly EfAiUsageRepository usage;
    private readonly AiCreditGate gate;

    public AiCreditsTests()
    {
        organisationRepository = new EfOrganisationRepository(db);
        organisationService = new OrganisationService(organisationRepository, new EfTenantMembershipWriter(db), new InMemoryAuditRepository(), new InMemoryCaseRepository());
        meter = new EfUsageMeter(db, organisationRepository);
        usage = new EfAiUsageRepository(db);
        gate = new AiCreditGate(meter, organisationRepository);
    }

    public void Dispose() => db.Dispose();

    [Theory]
    [InlineData("gpt-5.4", 3000, 300, 4)]       // a typical Kato reply: about US$0.012, so 4 credits at 3x
    [InlineData("iip-chat-gpt-5.4-2026", 12000, 1000, 14)] // a claim summary: about US$0.045, so 14 credits
    [InlineData("gpt-4.1-mini", 3000, 300, 1)]
    [InlineData("unknown-model", null, null, 1)]
    public void Tokens_become_credits_from_the_real_price_and_never_round_to_nothing(string model, int? prompt, int? completion, int expected)
    {
        var (_, credits) = AiCreditPricing.Default.Calculate(model, prompt, completion);

        Assert.Equal(expected, credits);
    }

    [Fact]
    public void The_dollar_cost_is_the_published_price_before_markup()
    {
        var (cost, _) = AiCreditPricing.Default.Calculate("gpt-5.4", 1_000_000, 1_000_000);

        Assert.Equal(17.5m, cost);
    }

    [Fact]
    public async Task Recording_a_call_keeps_the_feature_and_adds_its_credits_to_the_months_total()
    {
        var organisation = await organisationService.CreateAsync("owner-1", "Busy Insurer", OrganisationType.Insurer, "c");
        var recorder = Recorder(organisation.Scope, "kato");

        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 3000, 300);
        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 12000, 1000, "policy_answers");

        var status = await gate.StatusAsync(organisation.Scope);
        Assert.Equal(18, status.Used);
        var entries = await usage.ListAsync(organisation.Scope, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
        Assert.Equal(["kato", "policy_answers"], entries.OrderBy(e => e.Credits).Select(e => e.Feature));
    }

    [Fact]
    public async Task When_the_months_credits_are_used_up_the_next_call_is_refused_for_that_organisation_only()
    {
        var busy = await organisationService.CreateAsync("owner-1", "Busy Insurer", OrganisationType.Insurer, "c");
        var quiet = await organisationService.CreateAsync("owner-2", "Quiet Insurer", OrganisationType.Insurer, "c");
        await meter.AddAsync(busy.Scope, UsageKind.AiCredits, PlanCatalog.Trial.AiCreditsPerMonth);

        await Assert.ThrowsAsync<AiCreditsExhaustedException>(() => Recorder(busy.Scope, "kato").EnsureCreditsAsync());
        await Recorder(quiet.Scope, "kato").EnsureCreditsAsync();
        Assert.True((await gate.StatusAsync(busy.Scope)).Exhausted);
        Assert.False((await gate.StatusAsync(quiet.Scope)).Exhausted);
    }

    [Fact]
    public async Task A_call_that_pushes_past_the_limit_is_still_counted_and_only_the_next_is_stopped()
    {
        var organisation = await organisationService.CreateAsync("owner-1", "Busy Insurer", OrganisationType.Insurer, "c");
        await meter.AddAsync(organisation.Scope, UsageKind.AiCredits, PlanCatalog.Trial.AiCreditsPerMonth - 1);
        var recorder = Recorder(organisation.Scope, "kato");

        await recorder.EnsureCreditsAsync(); // one credit left: allowed
        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 12000, 1000); // costs 14

        Assert.True((await gate.StatusAsync(organisation.Scope)).Used > PlanCatalog.Trial.AiCreditsPerMonth);
        await Assert.ThrowsAsync<AiCreditsExhaustedException>(() => recorder.EnsureCreditsAsync());
    }

    [Fact]
    public async Task A_deployment_with_no_organisation_is_counted_but_never_stopped()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-operations");
        var recorder = Recorder(scope, "kato");

        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 3000, 300);
        await recorder.EnsureCreditsAsync();

        var status = await gate.StatusAsync(scope);
        Assert.False(status.Metered);
        Assert.False(status.Exhausted);
        Assert.Single(await usage.ListAsync(scope, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task A_call_with_no_workspace_in_scope_is_skipped_without_failing_the_answer()
    {
        var recorder = new AiUsageRecorder(usage, meter, gate, AiCreditPricing.Default, new AiUsageContext(), new NoContext(), NullLogger<AiUsageRecorder>.Instance);

        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 3000, 300);
        await recorder.EnsureCreditsAsync();
    }

    [Fact]
    public async Task The_summary_shows_use_by_feature_and_hides_cost_and_the_add_credits_action_from_people_without_billing()
    {
        var organisation = await organisationService.CreateAsync("owner-1", "Busy Insurer", OrganisationType.Insurer, "c");
        var recorder = Recorder(organisation.Scope, "kato");
        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 3000, 300);
        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 3000, 300, "command_center");
        await recorder.RecordAsync("AzureOpenAI", "gpt-5.4", 3000, 300, "command_center");

        var staff = await Summary(organisation.Scope, canSeeBilling: false).GetAsync();
        var admin = await Summary(organisation.Scope, canSeeBilling: true).GetAsync();

        Assert.True(staff.Metered);
        Assert.Equal(PlanCatalog.Trial.AiCreditsPerMonth, staff.Included);
        Assert.Equal(12, staff.Used);
        Assert.Equal("ok", staff.State);
        Assert.Equal(["Command Center", "Kato on WhatsApp"], staff.ByFeature.Select(f => f.Label));
        Assert.Null(staff.CostUsd);
        Assert.False(staff.CanAddCredits);
        Assert.Equal(0.036m, admin.CostUsd);
        Assert.True(admin.CanAddCredits);
    }

    [Theory]
    [InlineData(0.5, "ok")]
    [InlineData(0.95, "near_limit")]
    [InlineData(1.0, "exhausted")]
    public async Task The_meter_state_says_ok_near_the_limit_or_used_up(double fraction, string expected)
    {
        var organisation = await organisationService.CreateAsync("owner-1", "Busy Insurer", OrganisationType.Insurer, "c");
        await meter.AddAsync(organisation.Scope, UsageKind.AiCredits, (int)(PlanCatalog.Trial.AiCreditsPerMonth * fraction));

        var summary = await Summary(organisation.Scope, true).GetAsync();

        Assert.Equal(expected, summary.State);
    }

    [Fact]
    public async Task The_analyst_answers_with_figures_and_a_plain_notice_when_credits_are_used_up()
    {
        var organisation = await organisationService.CreateAsync("owner-1", "Busy Insurer", OrganisationType.Insurer, "c");
        await meter.AddAsync(organisation.Scope, UsageKind.AiCredits, PlanCatalog.Trial.AiCreditsPerMonth);
        var model = new ModelThatMustNotBeCalled();
        var analyst = new CommandCenterAnalyst(
            new FixedContext(organisation.Scope), new Permissions(canSeeBilling: false), new NoLeads(), new NoCases(), new NoCustomers(), new NoChannels(), model, gate);

        var answer = await analyst.AnswerAsync("How many prospects came in today?", TimeSpan.FromHours(3), "c-1");

        Assert.False(model.Called);
        Assert.False(answer.UsedModel);
        var notice = Assert.Single(answer.Blocks.OfType<CreditsNoticeBlock>());
        Assert.Contains("Ask your administrator", notice.Message);
        Assert.False(notice.CanAddCredits);
        Assert.Single(answer.Blocks.OfType<MetricPillsBlock>());
    }

    private AiUsageRecorder Recorder(TenantScope scope, string feature)
    {
        var context = new AiUsageContext();
        context.Use(feature, scope);
        return new AiUsageRecorder(usage, meter, gate, AiCreditPricing.Default, context, new NoContext(), NullLogger<AiUsageRecorder>.Instance);
    }

    private AiCreditsSummaryService Summary(TenantScope scope, bool canSeeBilling) =>
        new(new FixedContext(scope), new Permissions(canSeeBilling), gate, usage);

    private sealed class NoContext : ITenantContextAccessor
    {
        public TenantContext Current => throw new InvalidOperationException("No workspace in scope.");

        public bool HasCurrent => false;
    }

    private sealed class FixedContext(TenantScope scope) : ITenantContextAccessor
    {
        public TenantContext Current => new(scope.TenantId, scope.WorkspaceId, "user-1", ["Admin"]);

        public bool HasCurrent => true;
    }

    private sealed class Permissions(bool canSeeBilling) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(permission != Permission.ViewBilling || canSeeBilling);

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ModelThatMustNotBeCalled : InsuranceIntelligence.Application.Ai.IToolCallingChatProvider
    {
        public bool Called { get; private set; }

        public Task<InsuranceIntelligence.Application.Ai.AgentChatStep> NextAsync(
            IReadOnlyList<InsuranceIntelligence.Application.Ai.AgentChatMessage> messages,
            IReadOnlyList<InsuranceIntelligence.Application.Ai.AgentToolSpec> tools,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            throw new InvalidOperationException("The model was called with no credits left.");
        }
    }

    private sealed class NoLeads : InsuranceIntelligence.Application.SalesLeads.ISalesLeadReadRepository
    {
        public Task<InsuranceIntelligence.Application.SalesLeads.SalesLeadDetailReadModel?> GetLeadDetailAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InsuranceIntelligence.Application.SalesLeads.SalesLeadDetailReadModel?>(null);

        public Task<IReadOnlyList<InsuranceIntelligence.Application.SalesLeads.SalesLeadDetailReadModel>> ListLeadsAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InsuranceIntelligence.Application.SalesLeads.SalesLeadDetailReadModel>>([]);
    }

    private sealed class NoCases : InsuranceIntelligence.Domain.Cases.ICaseRepository
    {
        public Task SaveAsync(InsuranceIntelligence.Domain.Cases.ClaimCase claimCase, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<InsuranceIntelligence.Domain.Cases.ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InsuranceIntelligence.Domain.Cases.ClaimCase?>(null);

        public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Cases.ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Cases.ClaimCase>>([]);
    }

    private sealed class NoCustomers : InsuranceIntelligence.Domain.Customers.ICustomerRepository
    {
        public Task<InsuranceIntelligence.Domain.Customers.Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<InsuranceIntelligence.Domain.Customers.Customer?>(null);

        public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>>([]);

        public Task UpsertAsync(InsuranceIntelligence.Domain.Customers.Customer customer, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoChannels : InsuranceIntelligence.Application.Channels.IChannelReadRepository
    {
        public Task<IReadOnlyList<InsuranceIntelligence.Application.Channels.ChannelConversationReadModel>> GetOverviewAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InsuranceIntelligence.Application.Channels.ChannelConversationReadModel>>([]);
    }
}
