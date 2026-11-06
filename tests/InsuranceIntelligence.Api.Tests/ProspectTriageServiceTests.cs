using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ProspectTriageServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-operations");

    private static SalesLeadDetailReadModel Boda() =>
        new("L1", "WhatsApp", "need", "summary", "To be assessed", "New", [], [], null, null, CustomerName: "Brian", ContactInfo: "+256790000002", CreatedAt: DateTimeOffset.UtcNow)
        {
            Phone = "256790000002",
            Facts = [new ProspectFact("occupation", "boda boda rider", ProspectFactSource.Customer, DateTimeOffset.UtcNow)],
            Interests = [new ProspectInterest("Boda Rider Accident", InterestStrength.WantsIt, "asked", DateTimeOffset.UtcNow)],
        };

    [Fact]
    public async Task Scoring_for_a_system_caller_stores_the_score_without_a_signed_in_user()
    {
        var store = new Store(Boda());
        var service = new ProspectTriageService(new NoContext(), new DenyPermissions(), store, store, new NullAudit());

        var result = await service.ScoreAsync(Scope, "L1", "system:customer-assistant");

        Assert.Equal(1, result.Scored);
        var saved = Assert.IsType<ProspectTriage>(store.Lead.Triage);
        Assert.NotEmpty(saved.Fits);
        Assert.Contains(saved.Fits[0].Reasons, reason => reason.Contains("boda", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Scoring_by_staff_still_needs_the_permission()
    {
        var store = new Store(Boda());
        var service = new ProspectTriageService(new FixedContext(), new DenyPermissions(), store, store, new NullAudit());

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.RunAsync(null));
        Assert.Null(store.Lead.Triage);
    }

    [Fact]
    public async Task A_workspace_that_switched_the_triage_agent_off_is_not_scored()
    {
        var store = new Store(Boda());
        var service = new ProspectTriageService(new NoContext(), new DenyPermissions(), store, store, new NullAudit(), agentGate: new FixedGate(false));

        var result = await service.ScoreAsync(Scope, "L1", "system:customer-assistant");

        Assert.Equal(0, result.Scored);
        Assert.Null(store.Lead.Triage);
    }

    [Fact]
    public async Task A_workspace_that_left_the_triage_agent_on_is_scored()
    {
        var store = new Store(Boda());
        var service = new ProspectTriageService(new NoContext(), new DenyPermissions(), store, store, new NullAudit(), agentGate: new FixedGate(true));

        var result = await service.ScoreAsync(Scope, "L1", "system:customer-assistant");

        Assert.Equal(1, result.Scored);
        Assert.NotNull(store.Lead.Triage);
    }

    private sealed class FixedGate(bool enabled) : IAgentGate
    {
        public Task<AgentGateResult> CheckAsync(string agentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentGateResult(enabled, agentKey, agentKey, null, null, null));

        public Task<AgentGateResult> CheckAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentGateResult(enabled, agentKey, agentKey, null, null, null));
    }

    private sealed class NoContext : ITenantContextAccessor
    {
        public TenantContext Current => throw new InvalidOperationException("No signed-in user.");
        public bool HasCurrent => false;
    }

    private sealed class FixedContext : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-operations", "user-1", ["Viewer"]);
        public bool HasCurrent => true;
    }

    private sealed class DenyPermissions : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => throw new PermissionDeniedException(permission);
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NullAudit : IAuditWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Store(SalesLeadDetailReadModel lead) : ISalesLeadReadRepository, ISalesLeadWriteRepository
    {
        public SalesLeadDetailReadModel Lead { get; private set; } = lead;

        public Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SalesLeadDetailReadModel?>(Lead);

        public Task<IReadOnlyList<SalesLeadDetailReadModel>> ListLeadsAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesLeadDetailReadModel>>([Lead]);

        public Task CreateAsync(string tenantId, string workspaceId, SalesLeadDetailReadModel lead, CancellationToken cancellationToken = default)
        {
            Lead = lead;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SalesLeadDetailReadModel?> FindByPhoneAsync(string tenantId, string workspaceId, string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult<SalesLeadDetailReadModel?>(Lead);
    }
}
