using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Retention;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Retention;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class RetentionWatchServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);

    [Fact]
    public async Task GetWatchAsync_OnlyIncludesPoliciesWithinSixtyDays_BucketedCorrectly()
    {
        var (service, policies, customers, _, _, _, _) = Build();
        await customers.UpsertAsync(Customer("cust-1", "Aisha Nakato"), CancellationToken.None);
        await customers.UpsertAsync(Customer("cust-2", "Brian Okot"), CancellationToken.None);
        await customers.UpsertAsync(Customer("cust-3", "Carol Adong"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-14", "cust-1", Today.AddDays(10)), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-30", "cust-2", Today.AddDays(25)), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-outside", "cust-3", Today.AddDays(90)), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-no-expiry", "cust-3", null), CancellationToken.None);

        var summary = await service.GetWatchAsync();

        Assert.Equal(2, summary.PoliciesAtRisk);
        var item14 = Assert.Single(summary.Items, item => item.PolicyId == "pol-14");
        Assert.Equal(14, item14.ExpiryBucketDays);
        var item30 = Assert.Single(summary.Items, item => item.PolicyId == "pol-30");
        Assert.Equal(30, item30.ExpiryBucketDays);
    }

    [Fact]
    public async Task GetWatchAsync_MorePriorClaimsAndNoRecentContact_ScoresHigherAndRanksFirst()
    {
        var (service, policies, customers, cases, communications, _, _) = Build();
        await customers.UpsertAsync(Customer("cust-attention", "Attention Needed"), CancellationToken.None);
        await customers.UpsertAsync(Customer("cust-quiet", "Recently Contacted"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-attention", "cust-attention", Today.AddDays(10), "POL-ATTN"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-quiet", "cust-quiet", Today.AddDays(10), "POL-QUIET"), CancellationToken.None);

        await cases.SaveAsync(Claim("POL-ATTN"), CancellationToken.None);
        await communications.SaveAsync(Communication("cust-quiet", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);

        var summary = await service.GetWatchAsync();

        var attention = Assert.Single(summary.Items, item => item.PolicyId == "pol-attention");
        var quiet = Assert.Single(summary.Items, item => item.PolicyId == "pol-quiet");
        Assert.Equal(1, attention.ClaimCountLast12Months);
        Assert.Null(attention.DaysSinceLastContact);
        Assert.NotNull(quiet.DaysSinceLastContact);
        Assert.True(attention.AttentionScore > quiet.AttentionScore);
        Assert.Equal("pol-attention", summary.Items[0].PolicyId);
    }

    [Fact]
    public async Task DraftReminderAsync_CreatesAPendingApprovalDraftAddressedToTheCustomersPhone()
    {
        var (service, policies, customers, _, _, reminders, _) = Build();
        await customers.UpsertAsync(Customer("cust-1", "Aisha Nakato", phone: "+256772000111"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-1", "cust-1", Today.AddDays(14)), CancellationToken.None);

        var reminder = await service.DraftReminderAsync("pol-1");

        Assert.Equal("PendingApproval", reminder.Status);
        Assert.Equal("SMS", reminder.Channel);
        Assert.Equal("+256772000111", reminder.Recipient);
        Assert.Contains("Aisha Nakato", reminder.MessageBody);

        var stored = await reminders.ListForPolicyAsync("pol-1", Scope, CancellationToken.None);
        Assert.Single(stored);
    }

    [Fact]
    public async Task DraftReminderAsync_PolicyOutsideRetentionWindow_ThrowsValidation()
    {
        var (service, policies, customers, _, _, _, _) = Build();
        await customers.UpsertAsync(Customer("cust-1", "Aisha Nakato"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-1", "cust-1", Today.AddDays(120)), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<RetentionWatchValidationException>(() => service.DraftReminderAsync("pol-1"));
        Assert.Equal("policyId", ex.Field);
    }

    [Fact]
    public async Task ApproveAndSendAsync_ConsentPermitted_SendsAndRecordsDeliveryStatus()
    {
        var (service, policies, customers, _, _, reminders, sms) = Build();
        await customers.UpsertAsync(Customer("cust-1", "Aisha Nakato", phone: "+256772000111"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-1", "cust-1", Today.AddDays(14)), CancellationToken.None);
        var draft = await service.DraftReminderAsync("pol-1");
        sms.NextResult = new CommunicationSendResult("Sent", "provider-msg-1", null);

        var sent = await service.ApproveAndSendAsync(Guid.Parse(draft.ReminderId));

        Assert.Equal("Sent", sent.Status);
        Assert.NotNull(sent.SentAt);
        Assert.True(sms.LastRequest!.Approved);
        Assert.False(sms.LastRequest.LowRisk);

        var stored = (await reminders.ListForPolicyAsync("pol-1", Scope, CancellationToken.None)).Single();
        Assert.Equal(RetentionReminderStatus.Sent, stored.Status);
    }

    [Fact]
    public async Task ApproveAndSendAsync_ProviderReportsConsentRequired_RecordsConsentRequiredWithoutThrowing()
    {
        var (service, policies, customers, _, _, _, sms) = Build();
        await customers.UpsertAsync(Customer("cust-1", "Aisha Nakato", phone: "+256772000111"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-1", "cust-1", Today.AddDays(14)), CancellationToken.None);
        var draft = await service.DraftReminderAsync("pol-1");
        sms.NextResult = new CommunicationSendResult("ConsentRequired", string.Empty, "No recorded consent.");

        var sent = await service.ApproveAndSendAsync(Guid.Parse(draft.ReminderId));

        Assert.Equal("ConsentRequired", sent.Status);
        Assert.Null(sent.SentAt);
    }

    [Fact]
    public async Task ApproveAndSendAsync_AlreadySent_ThrowsValidation()
    {
        var (service, policies, customers, _, _, _, sms) = Build();
        await customers.UpsertAsync(Customer("cust-1", "Aisha Nakato", phone: "+256772000111"), CancellationToken.None);
        await policies.UpsertAsync(Policy("pol-1", "cust-1", Today.AddDays(14)), CancellationToken.None);
        var draft = await service.DraftReminderAsync("pol-1");
        sms.NextResult = new CommunicationSendResult("Sent", "provider-msg-1", null);
        await service.ApproveAndSendAsync(Guid.Parse(draft.ReminderId));

        var ex = await Assert.ThrowsAsync<RetentionWatchValidationException>(() => service.ApproveAndSendAsync(Guid.Parse(draft.ReminderId)));
        Assert.Equal("reminderId", ex.Field);
    }

    private static (
        IRetentionWatchService Service,
        InMemoryPolicyRepository Policies,
        InMemoryCustomerRepository Customers,
        InMemoryCaseRepository Cases,
        InMemoryCommunicationRepository Communications,
        InMemoryRetentionReminderRepository Reminders,
        FakeSmsProvider Sms) Build()
    {
        var context = new TenantContextAccessor();
        context.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, "officer-1", [PlatformRole.ClaimsSupervisor.ToString()]));
        var policies = new InMemoryPolicyRepository();
        var customers = new InMemoryCustomerRepository();
        var cases = new InMemoryCaseRepository();
        var communications = new InMemoryCommunicationRepository();
        var reminders = new InMemoryRetentionReminderRepository();
        var sms = new FakeSmsProvider();
        var email = new FakeEmailSendingProvider();
        var audit = new RecordingAuditWriter();
        var service = new RetentionWatchService(
            context, new AllowAllPermissionService(), policies, customers, cases, communications, reminders, sms, email, audit);
        return (service, policies, customers, cases, communications, reminders, sms);
    }

    private static InsuranceIntelligence.Domain.Customers.Customer Customer(string customerId, string fullName, string? phone = null, string? email = null) =>
        new(customerId, Scope.TenantId, Scope.WorkspaceId, fullName, email, phone);

    private static Policy Policy(string policyId, string customerId, DateOnly? effectiveTo, string? policyNumber = null) =>
        new(
            policyId, Scope.TenantId, Scope.WorkspaceId, policyNumber ?? policyId.ToUpperInvariant(), "Motor",
            null, customerId, null, Today.AddMonths(-6), effectiveTo, 500_000m, "UGX", 10_000_000m);

    private static ClaimCase Claim(string policyNumber) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = "Attention Needed",
            PolicyNumber = policyNumber,
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Email",
            IncidentDate = Today,
            IncidentDescription = "Prior claim on this policy.",
            Status = CaseStatus.Closed,
            CompletenessScore = 100,
            RiskLevel = "Low",
            Priority = CasePriority.Normal,
            CreatedByUserId = "officer-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-claim-1"
        };

    private static Communication Communication(string customerId, DateTimeOffset createdAt) =>
        new()
        {
            CommunicationId = Guid.NewGuid(),
            Scope = Scope,
            CaseId = Guid.NewGuid(),
            CustomerId = customerId,
            Channel = CommunicationChannel.SMS,
            Direction = "Outbound",
            MessageBody = "Prior contact.",
            SentBy = "officer-1",
            DeliveryStatus = "Sent",
            CreatedAt = createdAt,
            CorrelationId = "corr-comm-1"
        };

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Written { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Written.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSmsProvider : ISmsProvider
    {
        public CommunicationSendResult NextResult { get; set; } = new("Sent", "provider-msg", null);
        public CommunicationSendRequest? LastRequest { get; private set; }

        public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeEmailSendingProvider : IEmailSendingProvider
    {
        public CommunicationSendResult NextResult { get; set; } = new("Sent", "provider-msg", null);

        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(NextResult);
    }
}
