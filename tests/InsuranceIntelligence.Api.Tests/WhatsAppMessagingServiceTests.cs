using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WhatsAppMessagingServiceTests
{
    [Fact]
    public async Task SendAsync_UsesOneProviderAttemptForRepeatedIdempotencyKey()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Repository.SaveAsync(fixture.InboundMessage(DateTimeOffset.UtcNow.AddHours(-1)));

        var request = fixture.SendRequest("send-001");
        var first = await fixture.Service.SendAsync(request);
        var replay = await fixture.Service.SendAsync(request);

        Assert.Equal(WhatsAppDeliveryState.ProviderAccepted, first.DeliveryState);
        Assert.Equal(first.MessageId, replay.MessageId);
        Assert.Equal(1, fixture.Provider.Attempts);
    }

    [Fact]
    public async Task SendAsync_OutsideCustomerServiceWindow_RequiresApprovedTemplate()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Repository.SaveAsync(fixture.InboundMessage(DateTimeOffset.UtcNow.AddHours(-25)));

        var error = await Assert.ThrowsAsync<WhatsAppPolicyException>(() =>
            fixture.Service.SendAsync(fixture.SendRequest("send-002")));

        Assert.Equal("TemplateRequired", error.Code);
        Assert.Equal(0, fixture.Provider.Attempts);
    }

    [Fact]
    public async Task SendAsync_UnresolvedPreviousAttempt_BlocksNewIdempotencyKey()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Repository.SaveAsync(fixture.InboundMessage(DateTimeOffset.UtcNow.AddHours(-1)));
        await fixture.Repository.SaveAsync(fixture.InboundMessage(DateTimeOffset.UtcNow) with
        {
            MessageId = Guid.NewGuid(),
            Direction = WhatsAppMessageDirection.Outbound,
            DeliveryState = WhatsAppDeliveryState.Unknown,
            IdempotencyKey = "previous-send",
            ProviderMessageId = null
        });

        var error = await Assert.ThrowsAsync<WhatsAppPolicyException>(() =>
            fixture.Service.SendAsync(fixture.SendRequest("new-send")));

        Assert.Equal("ReconciliationRequired", error.Code);
        Assert.Equal(0, fixture.Provider.Attempts);
    }

    [Fact]
    public async Task ApplyDeliveryEventsAsync_IsDuplicateSafeAndNeverRegressesRead()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Repository.SaveAsync(fixture.InboundMessage(DateTimeOffset.UtcNow.AddHours(-1)));
        var sent = await fixture.Service.SendAsync(fixture.SendRequest("send-003"));

        await fixture.Service.ApplyDeliveryEventsAsync(fixture.Scope, [
            new WhatsAppDeliveryEvent("provider-message-1", "phone-1", WhatsAppDeliveryState.Read, DateTimeOffset.UtcNow, null, null),
            new WhatsAppDeliveryEvent("provider-message-1", "phone-1", WhatsAppDeliveryState.Delivered, DateTimeOffset.UtcNow.AddSeconds(-1), null, null),
            new WhatsAppDeliveryEvent("provider-message-1", "phone-1", WhatsAppDeliveryState.Read, DateTimeOffset.UtcNow, null, null)
        ]);

        var updated = await fixture.Repository.GetAsync(fixture.Scope, sent.MessageId);
        Assert.Equal(WhatsAppDeliveryState.Read, updated!.DeliveryState);
        Assert.NotNull(updated.DeliveredAt);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public void WebhookParser_ExtractsTextAndDeliveryStatus()
    {
        const string json = """
            {"entry":[{"changes":[{"value":{"metadata":{"phone_number_id":"phone-1"},
            "messages":[{"from":"256772118001","id":"wamid.in","timestamp":"1700000000","type":"text","text":{"body":"Hello"}}],
            "statuses":[{"id":"wamid.out","status":"delivered","timestamp":"1700000001"}]}}]}]}
            """;

        var inbound = WhatsAppWebhookParser.ParseMessages(json);
        var delivery = WhatsAppWebhookParser.ParseDeliveryEvents(json);

        Assert.Equal("Hello", Assert.Single(inbound).CustomerSafeBody);
        Assert.Equal(WhatsAppDeliveryState.Delivered, Assert.Single(delivery).DeliveryState);
    }

    private sealed class Fixture
    {
        private Fixture(
            TenantScope scope,
            Guid caseId,
            StubWhatsAppRepository repository,
            StubOutboundProvider provider,
            WhatsAppMessagingService service)
        {
            Scope = scope;
            CaseId = caseId;
            Repository = repository;
            Provider = provider;
            Service = service;
        }

        public TenantScope Scope { get; }
        public Guid CaseId { get; }
        public StubWhatsAppRepository Repository { get; }
        public StubOutboundProvider Provider { get; }
        public WhatsAppMessagingService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var scope = TenantScope.Create("tenant-wa", "workspace-wa");
            var caseId = Guid.NewGuid();
            var cases = new InMemoryCaseRepository();
            await cases.SaveAsync(new ClaimCase
            {
                CaseId = caseId,
                Scope = scope,
                CustomerName = "WhatsApp Customer",
                PolicyNumber = "POL-WA-1",
                ProductLine = "Motor",
                ClaimType = "Own damage",
                Channel = "WhatsApp",
                IncidentDate = new DateOnly(2026, 8, 1),
                IncidentDescription = "Test",
                Status = CaseStatus.InReview,
                CompletenessScore = 80,
                AssignedUserId = "operator-1",
                CreatedByUserId = "operator-1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CorrelationId = "case-correlation"
            });
            var context = new TenantContextAccessor();
            context.SetCurrent(new TenantContext(scope.TenantId, scope.WorkspaceId, "operator-1", []));
            var repository = new StubWhatsAppRepository();
            var provider = new StubOutboundProvider();
            var service = new WhatsAppMessagingService(
                cases,
                repository,
                provider,
                context,
                new StubPermissionService(Permission.ViewChannels, Permission.ManageCustomerCommunications, Permission.SendCustomerUpdate),
                new NullAuditWriter());
            return new Fixture(scope, caseId, repository, provider, service);
        }

        public WhatsAppMessage InboundMessage(DateTimeOffset receivedAt) => new()
        {
            MessageId = Guid.NewGuid(), Scope = Scope, CaseId = CaseId, ThreadId = WhatsAppMessagingService.BuildThreadId("phone-1", "256772118001"),
            BusinessPhoneNumberId = "phone-1", ContactWaId = "256772118001", Direction = WhatsAppMessageDirection.Inbound,
            ContentType = WhatsAppContentType.Text, CustomerSafeBody = "Customer message", ProviderMessageId = $"wamid.{Guid.NewGuid():N}",
            DeliveryState = WhatsAppDeliveryState.Received, CreatedAt = receivedAt, UpdatedAt = receivedAt,
            CorrelationId = "inbound-correlation"
        };

        public SendWhatsAppMessageCommand SendRequest(string idempotencyKey) => new(
            CaseId, WhatsAppMessagingService.BuildThreadId("phone-1", "256772118001"), "256772118001", "Your claim review is continuing.",
            "claim_status_update", "consent_recorded", "en", null, true, idempotencyKey, null);
    }

    private sealed class StubWhatsAppRepository : IWhatsAppMessageRepository
    {
        private readonly Dictionary<Guid, WhatsAppMessage> messages = [];
        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
        {
            messages[message.MessageId] = message;
            return Task.CompletedTask;
        }
        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(messages.GetValueOrDefault(messageId) is { } message && message.Scope.Matches(scope) ? message : null);
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(messages.Values.SingleOrDefault(message => message.Scope.Matches(scope) && message.ProviderMessageId == providerMessageId));
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(messages.Values.SingleOrDefault(message => message.Scope.Matches(scope) && message.IdempotencyKey == idempotencyKey));
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppMessage>>(messages.Values.Where(message => message.Scope.Matches(scope) && message.ThreadId == threadId).OrderBy(message => message.CreatedAt).ToArray());
    }

    private sealed class StubOutboundProvider : IWhatsAppOutboundProvider
    {
        public bool IsConfigured => true;
        public int Attempts { get; private set; }
        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(new WhatsAppProviderSendResult(true, "provider-message-1", null, null));
        }
    }

    private sealed class StubPermissionService(params Permission[] permissions) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(permissions.Contains(permission));
        public async Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default)
        {
            if (!await HasPermissionAsync(permission, cancellationToken)) throw new PermissionDeniedException(permission);
        }
    }

    private sealed class NullAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
