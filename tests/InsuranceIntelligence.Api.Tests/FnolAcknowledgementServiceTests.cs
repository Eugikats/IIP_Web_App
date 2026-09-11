using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Tests;

public sealed class FnolAcknowledgementServiceTests
{
    [Fact]
    public async Task SendAsync_WhatsAppChannel_SkipsBecauseAlreadyAcknowledgedInline()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WhatsApp", phone: "+256700000000");

        var outcome = await fixture.Service.SendAsync(claimCase, "corr-1");

        Assert.Equal(FnolAcknowledgementOutcome.SkippedAlreadyAcknowledgedInline, outcome);
        Assert.Empty(fixture.SendingProvider.SmsRequests);
        Assert.Empty(fixture.CommunicationRepository.Saved);
    }

    [Fact]
    public async Task SendAsync_NoPhoneOrEmail_ReturnsNoRecipientAvailableAndAudits()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WebPortal", phone: null, email: null);

        var outcome = await fixture.Service.SendAsync(claimCase, "corr-1");

        Assert.Equal(FnolAcknowledgementOutcome.NoRecipientAvailable, outcome);
        Assert.Empty(fixture.CommunicationRepository.Saved);
        var auditEvent = Assert.Single(fixture.AuditRepository.Events);
        Assert.Equal("fnol.acknowledgement", auditEvent.ActionName);
        Assert.Equal("NoRecipientAvailable", auditEvent.ValidationResult);
    }

    [Fact]
    public async Task SendAsync_PhonePresent_PrefersSmsAndIncludesReferenceAndRequiredDocuments()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WebPortal", phone: "+256700000000", email: "amina@example.test");

        var outcome = await fixture.Service.SendAsync(claimCase, "corr-1");

        Assert.Equal(FnolAcknowledgementOutcome.Sent, outcome);
        var request = Assert.Single(fixture.SendingProvider.SmsRequests);
        Assert.Empty(fixture.SendingProvider.EmailRequests); // phone present -> SMS preferred over email
        Assert.Contains(claimCase.CaseId.ToString()[..8].ToUpperInvariant(), request.MessageBody);
        Assert.Contains("Claim form", request.MessageBody);
        Assert.Contains("Police abstract", request.MessageBody);
        Assert.True(request.LowRisk);
        Assert.False(request.Approved);

        var communication = Assert.Single(fixture.CommunicationRepository.Saved);
        Assert.Equal(CommunicationChannel.SMS, communication.Channel);
        Assert.Equal("fnol-acknowledgement", communication.TemplateId);
        Assert.Equal(claimCase.CaseId, communication.CaseId);

        var auditEvent = Assert.Single(fixture.AuditRepository.Events);
        Assert.Equal("Sent", auditEvent.ValidationResult);
        Assert.Equal("system:fnol-acknowledgement", auditEvent.UserId);
        Assert.Equal(ActorType.Integration, auditEvent.ActorType);
    }

    [Fact]
    public async Task SendAsync_EmailOnlyNoPhone_FallsBackToEmail()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "Email", phone: null, email: "amina@example.test");

        var outcome = await fixture.Service.SendAsync(claimCase, "corr-1");

        Assert.Equal(FnolAcknowledgementOutcome.Sent, outcome);
        Assert.Single(fixture.SendingProvider.EmailRequests);
        Assert.Empty(fixture.SendingProvider.SmsRequests);
        var communication = Assert.Single(fixture.CommunicationRepository.Saved);
        Assert.Equal(CommunicationChannel.Email, communication.Channel);
    }

    [Fact]
    public async Task SendAsync_ProviderDeclines_ReturnsNotSentAndDoesNotPersistCommunication()
    {
        var fixture = Fixture.Create(sendResult: new CommunicationSendResult("HumanApprovalRequired", string.Empty, "Pending tenant opt-in."));
        var claimCase = fixture.Case(channel: "WebPortal", phone: "+256700000000");

        var outcome = await fixture.Service.SendAsync(claimCase, "corr-1");

        Assert.Equal(FnolAcknowledgementOutcome.NotSent, outcome);
        Assert.Empty(fixture.CommunicationRepository.Saved);
        var auditEvent = Assert.Single(fixture.AuditRepository.Events);
        Assert.Equal("HumanApprovalRequired", auditEvent.ValidationResult);
    }

    [Fact]
    public async Task SendAsync_NeverThrowsAndAlwaysReturnsAResult_EvenWhenChecklistIsEmpty()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WebPortal", phone: "+256700000000", productLine: "Unrecognized", claimType: "Unrecognized");

        var outcome = await fixture.Service.SendAsync(claimCase, "corr-1");

        Assert.Equal(FnolAcknowledgementOutcome.Sent, outcome);
    }

    private sealed class Fixture
    {
        private Fixture(FnolAcknowledgementService service, FakeCommunicationSendingProvider sendingProvider, FakeCommunicationRepository communicationRepository, FakeAuditRepository auditRepository)
        {
            Service = service;
            SendingProvider = sendingProvider;
            CommunicationRepository = communicationRepository;
            AuditRepository = auditRepository;
        }

        public FnolAcknowledgementService Service { get; }
        public FakeCommunicationSendingProvider SendingProvider { get; }
        public FakeCommunicationRepository CommunicationRepository { get; }
        public FakeAuditRepository AuditRepository { get; }

        public static Fixture Create(CommunicationSendResult? sendResult = null)
        {
            var sendingProvider = new FakeCommunicationSendingProvider(sendResult ?? new CommunicationSendResult("Sent", "provider-msg-1", null));
            var communicationRepository = new FakeCommunicationRepository();
            var auditRepository = new FakeAuditRepository();
            var checklistProvider = new StaticDocumentChecklistProvider(new CompletenessScoringService());
            var service = new FnolAcknowledgementService(checklistProvider, sendingProvider, communicationRepository, auditRepository);
            return new Fixture(service, sendingProvider, communicationRepository, auditRepository);
        }

        public ClaimCase Case(string channel, string? phone = "+256700000000", string? email = null, string productLine = "Motor", string claimType = "Accident") => new()
        {
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-fnol", "workspace-claims"),
            CustomerName = "Amina Okello",
            CustomerPhone = phone,
            CustomerEmail = email,
            PolicyNumber = "POL-1001",
            ProductLine = productLine,
            ClaimType = claimType,
            Channel = channel,
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Minor collision reported at branch intake.",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            CreatedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-fnol-seed",
        };
    }

    private sealed class FakeCommunicationSendingProvider(CommunicationSendResult result) : ICommunicationSendingProvider
    {
        public List<CommunicationSendRequest> EmailRequests { get; } = [];
        public List<CommunicationSendRequest> SmsRequests { get; } = [];

        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            EmailRequests.Add(request);
            return Task.FromResult(result);
        }

        public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            SmsRequests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeCommunicationRepository : ICommunicationRepository
    {
        public List<Communication> Saved { get; } = [];

        public Task SaveAsync(Communication communication, CancellationToken cancellationToken = default)
        {
            Saved.Add(communication);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Communication>>(Saved.Where(c => c.Scope.Matches(scope)).ToArray());

        public Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Communication>>(Saved.Where(c => c.CaseId == caseId && c.Scope.Matches(scope)).ToArray());

        public Task<Communication?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(c => c.Scope.Matches(scope) && c.ProviderMessageId == providerMessageId));
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.CaseId == caseId).ToArray());

        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.Scope.Matches(scope)).ToArray());
    }
}
