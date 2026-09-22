using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CaseFollowThroughServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task RunAsync_ClaimWithMissingDocuments_SendsAReminderAndStampsIt()
    {
        var deps = CreateDependencies();
        var claim = Claim(createdDaysAgo: 5);
        await deps.CaseRepository.SaveAsync(claim);

        var result = await deps.Service.RunAsync(Scope, "corr-1");

        Assert.Equal(1, result.MissingDocumentRemindersSent);
        var updated = await deps.CaseRepository.GetAsync(claim.CaseId);
        Assert.NotNull(updated!.LastMissingDocumentReminderAt);
    }

    [Fact]
    public async Task RunAsync_WithinTheWaitWindow_DoesNotSendASecondReminder()
    {
        var deps = CreateDependencies();
        await deps.CaseRepository.SaveAsync(Claim(createdDaysAgo: 1));

        var result = await deps.Service.RunAsync(Scope, "corr-1");

        Assert.Equal(0, result.MissingDocumentRemindersSent);
    }

    [Fact]
    public async Task RunAsync_SecondRunAfterAReminder_WaitsForTheFullWindowAgain()
    {
        var deps = CreateDependencies();
        await deps.CaseRepository.SaveAsync(Claim(createdDaysAgo: 5));

        var first = await deps.Service.RunAsync(Scope, "corr-1");
        var second = await deps.Service.RunAsync(Scope, "corr-2");

        Assert.Equal(1, first.MissingDocumentRemindersSent);
        Assert.Equal(0, second.MissingDocumentRemindersSent); // just reminded, stamped, not due again
    }

    [Fact]
    public async Task RunAsync_ClaimDueWithinALeadWindow_SendsAnSlaAtRiskWarning()
    {
        var deps = CreateDependencies();
        var claim = Claim(createdDaysAgo: 0) with { AssignedUserId = "officer-1", SlaDueAt = DateTimeOffset.UtcNow.AddHours(6) };
        await deps.CaseRepository.SaveAsync(claim);

        var result = await deps.Service.RunAsync(Scope, "corr-1");

        Assert.Equal(1, result.SlaWarningsSent);
        var notifications = await deps.NotificationRepository.ListForUserAsync(Scope, "officer-1", 10);
        Assert.Contains(notifications, n => n.Type == NotificationType.SlaAtRisk && n.CaseId == claim.CaseId);
    }

    [Fact]
    public async Task RunAsync_AlreadyBreached_DoesNotSendAWarning_ThatIsSlaBreachsJob()
    {
        var deps = CreateDependencies();
        var claim = Claim(createdDaysAgo: 0) with { AssignedUserId = "officer-1", SlaDueAt = DateTimeOffset.UtcNow.AddHours(-1) };
        await deps.CaseRepository.SaveAsync(claim);

        var result = await deps.Service.RunAsync(Scope, "corr-1");

        Assert.Equal(0, result.SlaWarningsSent);
    }

    [Fact]
    public async Task RunAsync_TooFarFromDue_DoesNotWarnYet()
    {
        var deps = CreateDependencies();
        var claim = Claim(createdDaysAgo: 0) with { AssignedUserId = "officer-1", SlaDueAt = DateTimeOffset.UtcNow.AddDays(5) };
        await deps.CaseRepository.SaveAsync(claim);

        var result = await deps.Service.RunAsync(Scope, "corr-1");

        Assert.Equal(0, result.SlaWarningsSent);
    }

    [Fact]
    public async Task RunAsync_SecondRun_NeverWarnsTwiceForTheSameClaim()
    {
        var deps = CreateDependencies();
        var claim = Claim(createdDaysAgo: 0) with { AssignedUserId = "officer-1", SlaDueAt = DateTimeOffset.UtcNow.AddHours(6) };
        await deps.CaseRepository.SaveAsync(claim);

        var first = await deps.Service.RunAsync(Scope, "corr-1");
        var second = await deps.Service.RunAsync(Scope, "corr-2");

        Assert.Equal(1, first.SlaWarningsSent);
        Assert.Equal(0, second.SlaWarningsSent);
        var notifications = await deps.NotificationRepository.ListForUserAsync(Scope, "officer-1", 10);
        Assert.Single(notifications);
    }

    [Fact]
    public async Task RunAsync_UnassignedClaim_NeverWarnsBecauseThereIsNoRecipient()
    {
        var deps = CreateDependencies();
        var claim = Claim(createdDaysAgo: 0) with { AssignedUserId = null, SlaDueAt = DateTimeOffset.UtcNow.AddHours(6) };
        await deps.CaseRepository.SaveAsync(claim);

        var result = await deps.Service.RunAsync(Scope, "corr-1");

        Assert.Equal(0, result.SlaWarningsSent);
    }

    private static ClaimCase Claim(int createdDaysAgo) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = "Amina Okello",
            CustomerPhone = "+256700000000",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Minor collision reported at branch intake.",
            Status = CaseStatus.Assigned,
            CompletenessScore = 0m,
            CreatedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-createdDaysAgo),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-createdDaysAgo),
            CorrelationId = "corr-follow-through-tests",
        };

    private static Dependencies CreateDependencies()
    {
        var caseRepository = new InMemoryCaseRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var auditRepository = new InMemoryAuditRepository();
        var notificationRepository = new InMemoryNotificationRepository();
        var notificationEmitter = new NotificationService(new TenantContextAccessor(), notificationRepository);
        var missingDocumentRequestService = new MissingDocumentRequestService(
            new NotConfiguredWhatsAppOutboundProvider(),
            new NoOpWhatsAppMessageRepository(),
            new AlwaysSucceedsCommunicationSendingProvider(),
            new NoOpCommunicationRepository(),
            new NoOpConsentRepository(),
            auditRepository);
        var service = new CaseFollowThroughService(
            caseRepository, documentRepository, missingDocumentRequestService, notificationEmitter, notificationRepository, auditRepository);

        return new Dependencies(service, caseRepository, notificationRepository);
    }

    private sealed record Dependencies(
        ICaseFollowThroughService Service,
        InMemoryCaseRepository CaseRepository,
        InMemoryNotificationRepository NotificationRepository);

    private sealed class NotConfiguredWhatsAppOutboundProvider : IWhatsAppOutboundProvider
    {
        public bool IsConfigured => false;
        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not configured, no claim here originates on WhatsApp.");
    }

    private sealed class NoOpWhatsAppMessageRepository : IWhatsAppMessageRepository
    {
        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should not be called, no claim here originates on WhatsApp.");
        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WhatsAppMessage>>([]);
    }

    private sealed class AlwaysSucceedsCommunicationSendingProvider : ICommunicationSendingProvider
    {
        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunicationSendResult("Sent", "fake-email", null));
        public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunicationSendResult("Sent", "fake-sms", null));
    }

    private sealed class NoOpCommunicationRepository : ICommunicationRepository
    {
        public Task SaveAsync(Communication communication, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Communication>>([]);
        public Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Communication>>([]);
        public Task<Communication?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult<Communication?>(null);
    }

    private sealed class NoOpConsentRepository : IConsentRepository
    {
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) => Task.FromResult<ConsentRecord?>(null);
        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ConsentRecord>>([]);
    }
}
