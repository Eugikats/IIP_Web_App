using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Tests;

public sealed class MissingDocumentRequestServiceTests
{
    [Fact]
    public async Task SendAsync_NoMissingDocuments_SkipsWithoutSendingAnything()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WebPortal", phone: "+256700000000");
        var checklist = fixture.Checklist(missing: []);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: null);

        Assert.Equal(MissingDocumentRequestOutcome.SkippedNoMissingDocuments, outcome);
        Assert.Empty(fixture.CommunicationProvider.SmsRequests);
        Assert.Empty(fixture.WhatsAppProvider.SentRequests);
        Assert.Empty(fixture.AuditRepository.Events);
    }

    [Fact]
    public async Task SendAsync_WhatsAppChannel_SendsOverWhatsAppNotSms()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WhatsApp", phone: "256700111222");
        var checklist = fixture.Checklist(missing: [DocumentType.PoliceAbstract]);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: "phone-id-1");

        Assert.Equal(MissingDocumentRequestOutcome.Sent, outcome);
        var sent = Assert.Single(fixture.WhatsAppProvider.SentRequests);
        Assert.Equal("phone-id-1", sent.BusinessPhoneNumberId);
        Assert.Equal("256700111222", sent.RecipientWaId);
        Assert.Contains("Police abstract", sent.CustomerSafeBody);
        Assert.Empty(fixture.CommunicationProvider.SmsRequests);
        var message = Assert.Single(fixture.WhatsAppMessageRepository.Saved);
        Assert.Equal("missing_document_request", message.Purpose);
    }

    [Fact]
    public async Task SendAsync_WhatsAppChannel_NoBusinessPhoneNumberId_ReturnsNoRecipientAvailable()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WhatsApp", phone: "256700111222");
        var checklist = fixture.Checklist(missing: [DocumentType.PoliceAbstract]);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: null);

        Assert.Equal(MissingDocumentRequestOutcome.NoRecipientAvailable, outcome);
        Assert.Empty(fixture.WhatsAppProvider.SentRequests);
    }

    [Fact]
    public async Task SendAsync_WhatsAppChannel_ProviderNotConfigured_ReturnsNotSent()
    {
        var fixture = Fixture.Create(whatsAppConfigured: false);
        var claimCase = fixture.Case(channel: "WhatsApp", phone: "256700111222");
        var checklist = fixture.Checklist(missing: [DocumentType.PoliceAbstract]);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: "phone-id-1");

        Assert.Equal(MissingDocumentRequestOutcome.NotSent, outcome);
        Assert.Empty(fixture.WhatsAppMessageRepository.Saved);
    }

    [Fact]
    public async Task SendAsync_WhatsAppChannel_NoActiveConsent_NeverSends()
    {
        // WO 6.1: this send has no fresh inbound message to lean on (triggered by workflow
        // evaluation, not a live webhook), a prior captured consent is the only thing that can
        // permit it, and here there is none.
        var fixture = Fixture.Create(whatsAppConsentGranted: false);
        var claimCase = fixture.Case(channel: "WhatsApp", phone: "256700111222");
        var checklist = fixture.Checklist(missing: [DocumentType.PoliceAbstract]);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: "phone-id-1");

        Assert.Equal(MissingDocumentRequestOutcome.NoActiveConsent, outcome);
        Assert.Empty(fixture.WhatsAppProvider.SentRequests);
        Assert.Empty(fixture.WhatsAppMessageRepository.Saved);
    }

    [Fact]
    public async Task SendAsync_NonWhatsAppChannel_PrefersSms()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "WebPortal", phone: "+256700000000", email: "amina@example.test");
        var checklist = fixture.Checklist(missing: [DocumentType.Invoice]);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: null);

        Assert.Equal(MissingDocumentRequestOutcome.Sent, outcome);
        var request = Assert.Single(fixture.CommunicationProvider.SmsRequests);
        Assert.Contains("Invoice", request.MessageBody);
        Assert.Empty(fixture.CommunicationProvider.EmailRequests);
        Assert.Empty(fixture.WhatsAppProvider.SentRequests);
        var communication = Assert.Single(fixture.CommunicationRepository.Saved);
        Assert.Equal(CommunicationChannel.SMS, communication.Channel);
    }

    [Fact]
    public async Task SendAsync_NonWhatsAppChannel_NoContact_ReturnsNoRecipientAvailable()
    {
        var fixture = Fixture.Create();
        var claimCase = fixture.Case(channel: "Email", phone: null, email: null);
        var checklist = fixture.Checklist(missing: [DocumentType.Invoice]);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: null);

        Assert.Equal(MissingDocumentRequestOutcome.NoRecipientAvailable, outcome);
        var auditEvent = Assert.Single(fixture.AuditRepository.Events);
        Assert.Equal("NoRecipientAvailable", auditEvent.ValidationResult);
    }

    [Fact]
    public async Task SendAsync_ProviderDeclines_ReturnsNotSentAndDoesNotPersistCommunication()
    {
        var fixture = Fixture.Create(sendResult: new CommunicationSendResult("HumanApprovalRequired", string.Empty, "Pending tenant opt-in."));
        var claimCase = fixture.Case(channel: "WebPortal", phone: "+256700000000");
        var checklist = fixture.Checklist(missing: [DocumentType.Invoice]);

        var outcome = await fixture.Service.SendAsync(claimCase, checklist, "corr-1", whatsAppBusinessPhoneNumberId: null);

        Assert.Equal(MissingDocumentRequestOutcome.NotSent, outcome);
        Assert.Empty(fixture.CommunicationRepository.Saved);
    }

    private sealed class Fixture
    {
        private Fixture(
            MissingDocumentRequestService service,
            FakeWhatsAppOutboundProvider whatsAppProvider,
            FakeWhatsAppMessageRepository whatsAppMessageRepository,
            FakeCommunicationSendingProvider communicationProvider,
            FakeCommunicationRepository communicationRepository,
            FakeAuditRepository auditRepository)
        {
            Service = service;
            WhatsAppProvider = whatsAppProvider;
            WhatsAppMessageRepository = whatsAppMessageRepository;
            CommunicationProvider = communicationProvider;
            CommunicationRepository = communicationRepository;
            AuditRepository = auditRepository;
        }

        public MissingDocumentRequestService Service { get; }
        public FakeWhatsAppOutboundProvider WhatsAppProvider { get; }
        public FakeWhatsAppMessageRepository WhatsAppMessageRepository { get; }
        public FakeCommunicationSendingProvider CommunicationProvider { get; }
        public FakeCommunicationRepository CommunicationRepository { get; }
        public FakeAuditRepository AuditRepository { get; }

        // Defaults to granted so every existing WhatsApp-success test proves what it means to
        // prove (send composition, channel selection) without also having to seed consent by
        // hand; whatsAppConsentGranted: false is for the one test that specifically proves
        // enforcement blocks a send with no active grant.
        public static Fixture Create(bool whatsAppConfigured = true, CommunicationSendResult? sendResult = null, bool whatsAppConsentGranted = true)
        {
            var whatsAppProvider = new FakeWhatsAppOutboundProvider(whatsAppConfigured);
            var whatsAppMessageRepository = new FakeWhatsAppMessageRepository();
            var communicationProvider = new FakeCommunicationSendingProvider(sendResult ?? new CommunicationSendResult("Sent", "provider-msg-1", null));
            var communicationRepository = new FakeCommunicationRepository();
            var consentRepository = new FakeConsentRepository(whatsAppConsentGranted);
            var auditRepository = new FakeAuditRepository();
            var service = new MissingDocumentRequestService(whatsAppProvider, whatsAppMessageRepository, communicationProvider, communicationRepository, consentRepository, auditRepository);
            return new Fixture(service, whatsAppProvider, whatsAppMessageRepository, communicationProvider, communicationRepository, auditRepository);
        }

        public ClaimCase Case(string channel, string? phone, string? email = null) => new()
        {
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-mdr", "workspace-claims"),
            CustomerName = "Amina Okello",
            CustomerPhone = phone,
            CustomerEmail = email,
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = channel,
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Minor collision reported at branch intake.",
            Status = CaseStatus.MissingDocuments,
            CompletenessScore = 30m,
            CreatedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-mdr-seed",
        };

        public DocumentCompletenessChecklist Checklist(IReadOnlyCollection<DocumentType> missing)
        {
            IReadOnlyCollection<DocumentType> required = [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.Invoice];
            IReadOnlyCollection<DocumentType> received = required.Except(missing).ToArray();
            return new DocumentCompletenessChecklist(
                Guid.NewGuid(),
                "Motor",
                required,
                [DocumentType.CustomerId],
                ReceivedDocuments: received,
                MissingRequiredDocuments: missing.Select(type => new MissingDocumentRequirement(type, LabelFor(type), true)).ToArray(),
                CompletenessScore: CompletenessScore.Calculate(required, received));
        }

        private static string LabelFor(DocumentType type) => type switch
        {
            DocumentType.ClaimForm => "Claim form",
            DocumentType.PoliceAbstract => "Police abstract",
            DocumentType.Invoice => "Invoice",
            DocumentType.CustomerId => "Customer ID",
            _ => "Supporting document",
        };
    }

    private sealed class FakeWhatsAppOutboundProvider(bool isConfigured = true) : IWhatsAppOutboundProvider
    {
        public bool IsConfigured => isConfigured;
        public List<WhatsAppProviderSendRequest> SentRequests { get; } = [];
        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            return Task.FromResult(new WhatsAppProviderSendResult(true, $"wamid.{Guid.NewGuid():N}", null, null));
        }
    }

    private sealed class FakeConsentRepository(bool granted) : IConsentRepository
    {
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(granted
                ? new ConsentRecord
                {
                    RecordId = Guid.NewGuid(), Scope = scope, CustomerIdentifier = customerIdentifier,
                    Basis = ConsentBasis.CustomerInitiatedConversation, Purpose = purpose, Channel = "WhatsApp",
                    RecordedByUserId = "system:customer-assistant", RecordedAt = DateTimeOffset.UtcNow, CorrelationId = "corr-consent-seed",
                }
                : null);
        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ConsentRecord>>([]);
    }

    private sealed class FakeWhatsAppMessageRepository : IWhatsAppMessageRepository
    {
        public List<WhatsAppMessage> Saved { get; } = [];
        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
        {
            Saved.Add(message);
            return Task.CompletedTask;
        }
        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(m => m.MessageId == messageId));
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(m => m.ProviderMessageId == providerMessageId));
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(m => m.IdempotencyKey == idempotencyKey));
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WhatsAppMessage>>(Saved.Where(m => m.ThreadId == threadId).ToArray());
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
        public Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Communication>>(Saved.Where(c => c.Scope.Matches(scope)).ToArray());
        public Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Communication>>(Saved.Where(c => c.CaseId == caseId && c.Scope.Matches(scope)).ToArray());
        public Task<Communication?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(c => c.Scope.Matches(scope) && c.ProviderMessageId == providerMessageId));
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
