using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Api.Tests;

public sealed class AcquisitionReportServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-operations");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CustomerConversationTurn Turn(string thread, CustomerAssistantOutcome outcome, int minutesAgo, string inbound = "hello", string? human = null) => new()
    {
        TurnId = Guid.NewGuid(),
        Scope = Scope,
        ThreadId = thread,
        ContactWaId = "256790000001",
        InboundMessageText = inbound,
        ClassifiedIntent = CustomerIntent.ProductInterest,
        Outcome = outcome,
        OccurredAt = Now.AddMinutes(-minutesAgo),
        HumanUserId = human,
        CorrelationId = "test",
    };

    private static SalesLeadDetailReadModel Lead(string id, string thread, bool? bank = true, ReadinessBand? band = null) =>
        new(id, "WhatsApp", "need", "summary", "To be assessed", "New", [], [], null, null, CustomerName: "Test Person", ContactInfo: "+256790000001", CreatedAt: Now.AddDays(-1))
        {
            Phone = "256790000001",
            ThreadId = thread,
            BankCustomer = bank,
            PrimaryChannel = "WhatsApp",
            Triage = band is null ? null : new ProspectTriage(Now, 70, band.Value, "High", [], [], []),
        };

    private static AcquisitionReportService Service(
        IReadOnlyList<SalesLeadDetailReadModel> leads,
        IReadOnlyList<CustomerConversationTurn> turns,
        IReadOnlyCollection<AgentRun>? runs = null) =>
        new(new FixedContext(), new AllowPermissions(), new Leads(leads), new Turns(turns), new Runs(runs ?? []), new OpenGate());

    [Fact]
    public async Task A_thread_Kato_handed_over_is_an_open_escalation_with_the_prospects_priority()
    {
        var report = await Service(
            [Lead("L1", "wa:t1", band: ReadinessBand.HighPriority)],
            [Turn("wa:t1", CustomerAssistantOutcome.Escalated, 30)]).EscalationsAsync();

        var row = Assert.Single(report.Rows);
        Assert.Equal("Open", row.Status);
        Assert.Equal("High", row.Priority);
        Assert.Equal(1, report.Open);
        Assert.Equal(1, report.HighPriority);
        Assert.Equal(1, report.Unassigned);
    }

    [Fact]
    public async Task Once_a_person_replies_the_escalation_waits_on_the_customer()
    {
        var report = await Service(
            [Lead("L1", "wa:t1")],
            [Turn("wa:t1", CustomerAssistantOutcome.Escalated, 30), Turn("wa:t1", CustomerAssistantOutcome.HumanReplied, 10, human: "u1")]).EscalationsAsync();

        Assert.Equal("Pending customer", Assert.Single(report.Rows).Status);
        Assert.Equal(1, report.PendingCustomer);
        Assert.Equal(0, report.Open);
    }

    [Fact]
    public async Task Handing_the_conversation_back_closes_the_escalation()
    {
        var report = await Service(
            [Lead("L1", "wa:t1")],
            [Turn("wa:t1", CustomerAssistantOutcome.Escalated, 30), Turn("wa:t1", CustomerAssistantOutcome.HandedBackToKato, 5, human: "u1")]).EscalationsAsync();

        Assert.Equal("Closed", Assert.Single(report.Rows).Status);
        Assert.Equal(0, report.Unassigned);
    }

    [Fact]
    public async Task A_conversation_that_never_escalated_is_not_listed()
    {
        var report = await Service([Lead("L1", "wa:t1")], [Turn("wa:t1", CustomerAssistantOutcome.AutoReplied, 30)]).EscalationsAsync();

        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task Non_customers_of_the_bank_are_counted_as_new_to_bank()
    {
        var report = await Service(
            [Lead("L1", "wa:t1", bank: false)],
            [Turn("wa:t1", CustomerAssistantOutcome.Escalated, 30)]).EscalationsAsync();

        Assert.Equal(1, report.NewToBank);
    }

    [Fact]
    public async Task An_agent_with_no_runs_says_so_and_a_failed_last_run_shows_errors()
    {
        var failed = new AgentRun(Guid.NewGuid(), Scope, ProspectTriageService.AgentKey, "triage_all", [], AgentRunStatus.Failed, Now, Now, "x", "c", "u", null, []);
        var report = await Service([], [], [failed]).AgentOperationsAsync();

        Assert.Equal("Errors", report.Rows.Single(row => row.Key == ProspectTriageService.AgentKey).Health);
        Assert.Equal("No runs yet", report.Rows.First(row => row.Key != ProspectTriageService.AgentKey).Health);
        Assert.Equal(1, report.Errors);
        Assert.Equal(3, report.NoRuns);
    }

    [Fact]
    public async Task Channel_intelligence_counts_leads_qualified_and_conversion_from_records_only()
    {
        var applied = Lead("L2", "wa:t2", band: ReadinessBand.Warm) with { AppliedFor = "Boda Rider Accident" };
        var report = await Service(
            [Lead("L1", "wa:t1", band: ReadinessBand.Qualified), applied],
            [Turn("wa:t1", CustomerAssistantOutcome.AutoReplied, 30), Turn("wa:t2", CustomerAssistantOutcome.AutoReplied, 20)]).ChannelIntelligenceAsync();

        Assert.Equal(2, report.NewLeads);
        Assert.Equal(1, report.Qualified);
        Assert.Equal(2, report.TotalInteractions);
        Assert.Equal(50m, report.ConversionPercent);
    }

    [Fact]
    public async Task With_no_leads_conversion_is_not_a_number()
    {
        var report = await Service([], []).ChannelIntelligenceAsync();

        Assert.Null(report.ConversionPercent);
    }

    private sealed class FixedContext : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-operations", "user-1", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class AllowPermissions : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class OpenGate : IAgentGate
    {
        public Task<AgentGateResult> CheckAsync(string agentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentGateResult(true, agentKey, agentKey, null, null, null));

        public Task<AgentGateResult> CheckAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) => CheckAsync(agentKey, cancellationToken);
    }

    private sealed class Leads(IReadOnlyList<SalesLeadDetailReadModel> leads) : ISalesLeadReadRepository
    {
        public Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default) =>
            Task.FromResult(leads.FirstOrDefault(lead => lead.LeadId == leadId));

        public Task<IReadOnlyList<SalesLeadDetailReadModel>> ListLeadsAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(leads);
    }

    private sealed class Turns(IReadOnlyList<CustomerConversationTurn> turns) : IConversationActivityReader
    {
        public Task<IReadOnlyList<CustomerConversationTurn>> ListTurnsAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(turns);
    }

    private sealed class Runs(IReadOnlyCollection<AgentRun> runs) : IAgentRunRepository
    {
        public Task SaveAsync(AgentRun run, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AgentRun?> GetAsync(Guid agentRunId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<AgentRun?>(null);
        public Task<IReadOnlyCollection<AgentRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(runs);
        public Task<IReadOnlyCollection<AgentRun>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(runs);
    }
}
