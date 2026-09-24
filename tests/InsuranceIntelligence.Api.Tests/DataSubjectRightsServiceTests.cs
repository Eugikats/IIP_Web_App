using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class DataSubjectRightsServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-dsr", "workspace-claims");

    [Fact]
    public async Task ExportAsync_UnknownCustomer_ReturnsNull()
    {
        var fixture = await Fixture.CreateAsync();

        var export = await fixture.Service.ExportAsync(Scope, "no-such-customer", whatsAppBusinessPhoneNumberId: null);

        Assert.Null(export);
    }

    [Fact]
    public async Task ExportAsync_AggregatesCustomerPoliciesCasesAndCommunications()
    {
        var fixture = await Fixture.CreateAsync();
        var customer = await fixture.SeedCustomerAsync("cust-1", "Amina Okello", "amina@example.test", "+256700000000");
        await fixture.SeedCaseAsync(customer, CaseStatus.InReview);

        var export = await fixture.Service.ExportAsync(Scope, "cust-1", whatsAppBusinessPhoneNumberId: null);

        Assert.NotNull(export);
        Assert.Equal("Amina Okello", export!.Customer.FullName);
        Assert.Single(export.Cases);
    }

    [Fact]
    public async Task RectifyAsync_UpdatesOnlyTheSpecifiedFields_AndWritesAnAuditEvent()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.SeedCustomerAsync("cust-1", "Amina Okello", "amina@example.test", "+256700000000");

        var rectified = await fixture.Service.RectifyAsync(
            Scope, "cust-1", new DataSubjectRectificationRequest(FullName: "Amina O. Nabatanzi", Email: null, Phone: null),
            "compliance-officer-1", "Customer called to correct a misspelled surname.", "corr-dsr-1");

        Assert.NotNull(rectified);
        Assert.Equal("Amina O. Nabatanzi", rectified!.FullName);
        Assert.Equal("amina@example.test", rectified.Email); // untouched, null in the request means "leave as is"
        var auditEvent = Assert.Single(fixture.Audit.Events);
        Assert.Equal("data_subject.rectified", auditEvent.ActionName);
        Assert.Equal("compliance-officer-1", auditEvent.UserId);
    }

    [Fact]
    public async Task RectifyAsync_UnknownCustomer_ReturnsNull()
    {
        var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.RectifyAsync(
            Scope, "no-such-customer", new DataSubjectRectificationRequest(null, null, null), "actor", "reason", "corr-dsr-2");

        Assert.Null(result);
    }

    [Fact]
    public async Task RequestErasureAsync_NoOpenCases_AnonymizesTheCustomerAndWritesAnAuditEvent()
    {
        var fixture = await Fixture.CreateAsync();
        var customer = await fixture.SeedCustomerAsync("cust-1", "Amina Okello", "amina@example.test", "+256700000000");
        await fixture.SeedCaseAsync(customer, CaseStatus.Closed);

        var result = await fixture.Service.RequestErasureAsync(Scope, "cust-1", "compliance-officer-1", "Customer requested erasure.", "corr-dsr-3");

        Assert.NotNull(result);
        Assert.Equal(DataSubjectErasureOutcome.Erased, result!.Outcome);
        var stored = await fixture.Customers.GetAsync("cust-1", Scope, CancellationToken.None);
        Assert.Equal("Erased customer", stored!.FullName);
        Assert.Null(stored.Email);
        Assert.Null(stored.Phone);
        var auditEvent = Assert.Single(fixture.Audit.Events);
        Assert.Equal("data_subject.erased", auditEvent.ActionName);
    }

    [Fact]
    public async Task RequestErasureAsync_HasAnOpenCase_RefusesWithADocumentedReason_AndLeavesTheCustomerUnchanged()
    {
        var fixture = await Fixture.CreateAsync();
        var customer = await fixture.SeedCustomerAsync("cust-1", "Amina Okello", "amina@example.test", "+256700000000");
        await fixture.SeedCaseAsync(customer, CaseStatus.InReview);

        var result = await fixture.Service.RequestErasureAsync(Scope, "cust-1", "compliance-officer-1", "Customer requested erasure.", "corr-dsr-4");

        Assert.NotNull(result);
        Assert.Equal(DataSubjectErasureOutcome.RefusedActiveCase, result!.Outcome);
        Assert.Contains("still open", result.Reason, StringComparison.OrdinalIgnoreCase);
        var stored = await fixture.Customers.GetAsync("cust-1", Scope, CancellationToken.None);
        Assert.Equal("Amina Okello", stored!.FullName); // untouched by the refusal
        var auditEvent = Assert.Single(fixture.Audit.Events);
        Assert.Equal("data_subject.erasure_refused", auditEvent.ActionName);
    }

    [Fact]
    public async Task RequestErasureAsync_OnlyTerminalStatusCases_IsNotBlocked()
    {
        var fixture = await Fixture.CreateAsync();
        var customer = await fixture.SeedCustomerAsync("cust-1", "Amina Okello", "amina@example.test", "+256700000000");
        await fixture.SeedCaseAsync(customer, CaseStatus.Settled);
        await fixture.SeedCaseAsync(customer, CaseStatus.Closed);

        var result = await fixture.Service.RequestErasureAsync(Scope, "cust-1", "compliance-officer-1", "reason", "corr-dsr-5");

        Assert.Equal(DataSubjectErasureOutcome.Erased, result!.Outcome);
    }

    private sealed class Fixture
    {
        private Fixture(DataSubjectRightsService service, InMemoryCustomerRepository customers, InMemoryCaseRepository cases, FakeAuditRepository audit)
        {
            Service = service;
            Customers = customers;
            Cases = cases;
            Audit = audit;
        }

        public DataSubjectRightsService Service { get; }
        public InMemoryCustomerRepository Customers { get; }
        public InMemoryCaseRepository Cases { get; }
        public FakeAuditRepository Audit { get; }

        public static Task<Fixture> CreateAsync()
        {
            var customers = new InMemoryCustomerRepository();
            var cases = new InMemoryCaseRepository();
            var policies = new InMemoryPolicyRepository();
            var communications = new FakeCommunicationRepository();
            var consent = new FakeConsentRepository();
            var conversations = new FakeConversationRepository();
            var audit = new FakeAuditRepository();
            var service = new DataSubjectRightsService(customers, policies, cases, communications, consent, conversations, audit);
            return Task.FromResult(new Fixture(service, customers, cases, audit));
        }

        public async Task<InsuranceIntelligence.Domain.Customers.Customer> SeedCustomerAsync(string customerId, string fullName, string? email, string? phone)
        {
            var customer = new InsuranceIntelligence.Domain.Customers.Customer(customerId, Scope.TenantId, Scope.WorkspaceId, fullName, email, phone);
            await Customers.UpsertAsync(customer, CancellationToken.None);
            return customer;
        }

        public async Task<ClaimCase> SeedCaseAsync(InsuranceIntelligence.Domain.Customers.Customer customer, CaseStatus status)
        {
            var claimCase = new ClaimCase
            {
                CaseId = Guid.NewGuid(),
                Scope = Scope,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                PolicyNumber = "POL-1001",
                ProductLine = "Motor",
                ClaimType = "Accident",
                Channel = "WebPortal",
                IncidentDate = new DateOnly(2026, 5, 24),
                IncidentDescription = "Minor collision.",
                Status = status,
                CompletenessScore = 50m,
                CreatedByUserId = "user-claims-1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CorrelationId = "corr-dsr-seed",
            };
            await Cases.SaveAsync(claimCase);
            return claimCase;
        }
    }

    private sealed class FakeCommunicationRepository : ICommunicationRepository
    {
        public Task SaveAsync(Communication communication, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Communication>>([]);
        public Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Communication>>([]);
        public Task<Communication?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult<Communication?>(null);
    }

    private sealed class FakeConsentRepository : IConsentRepository
    {
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) => Task.FromResult<ConsentRecord?>(null);
        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ConsentRecord>>([]);
    }

    private sealed class FakeConversationRepository : ICustomerConversationRepository
    {
        public Task SaveAsync(CustomerConversationTurn turn, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<CustomerConversationTurn>>([]);
        public Task<RetentionDeletionSummary> DeleteOlderThanAsync(TenantScope scope, DateTimeOffset cutoff, CancellationToken cancellationToken = default) => Task.FromResult(RetentionDeletionSummary.None);
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.CaseId == caseId).ToArray());
        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.Scope.Matches(scope)).ToArray());
    }
}
