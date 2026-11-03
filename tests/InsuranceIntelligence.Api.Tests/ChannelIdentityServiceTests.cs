using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Integrations;
using CustomerRecord = InsuranceIntelligence.Domain.Customers.Customer;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ChannelIdentityServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("insurer-a", "claims");
    private static readonly string CustomerId = Guid.NewGuid().ToString();
    private const string RecipientId = "wa-opaque-user-9182";

    [Fact]
    public async Task Link_RequiresExplicitCrossChannelConsentBeforeAnyWrite()
    {
        var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<ChannelIdentityPolicyException>(() => fixture.Service.LinkAsync(
            new LinkChannelIdentityCommand("whatsapp", "thread-1", CustomerId, false, "consent-log-55", "Verified with claimant")));

        Assert.Equal("ConsentRequired", exception.Code);
        Assert.Empty(fixture.Links.Items);
        Assert.Empty(fixture.Consents.Items);
        Assert.Empty(fixture.Audit.Events);
    }

    [Fact]
    public async Task Link_DerivesRecipientFromInboundMessageAndAuditsWithoutExternalIdentifier()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.LinkAsync(new LinkChannelIdentityCommand(
            "whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant"));

        Assert.True(result.IsActive);
        Assert.Equal(CustomerId, result.CustomerId);
        Assert.Equal("Amina Example", result.CustomerName);
        Assert.Single(fixture.Links.Items);
        Assert.Equal(ConsentBasis.ExplicitOptIn, Assert.Single(fixture.Consents.Items).Basis);
        Assert.Equal(ChannelIdentityConsentPurposes.ForChannel("whatsapp"), fixture.Consents.Items[0].Purpose);
        var audit = Assert.Single(fixture.Audit.Events);
        Assert.Equal("channel.identity.linked", audit.ActionName);
        Assert.DoesNotContain(RecipientId, audit.ValidationResult, StringComparison.Ordinal);
        Assert.Equal("Verified with claimant", audit.AuditReason);
    }

    [Fact]
    public async Task Link_RejectsCustomerOutsideResolvedWorkspace()
    {
        var fixture = new Fixture(customerExists: false);

        var exception = await Assert.ThrowsAsync<ChannelIdentityPolicyException>(() => fixture.Service.LinkAsync(
            new LinkChannelIdentityCommand("whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant")));

        Assert.Equal("CustomerNotFound", exception.Code);
        Assert.Empty(fixture.Links.Items);
    }

    [Fact]
    public async Task Link_RequiresAnActualInboundMessageOnTheCurrentAccount()
    {
        var fixture = new Fixture(hasInboundMessage: false);

        var exception = await Assert.ThrowsAsync<ChannelIdentityPolicyException>(() => fixture.Service.LinkAsync(
            new LinkChannelIdentityCommand("whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant")));

        Assert.Equal("InboundConversationRequired", exception.Code);
        Assert.Empty(fixture.Links.Items);
    }

    [Fact]
    public async Task Revoke_WithdrawsChannelContextConsentAndRecordsAudit()
    {
        var fixture = new Fixture();
        var created = await fixture.Service.LinkAsync(new LinkChannelIdentityCommand(
            "whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant"));

        var revoked = await fixture.Service.RevokeAsync(created.LinkId, "Customer withdrew permission");

        Assert.False(revoked.IsActive);
        Assert.Equal(DateTimeOffset.UtcNow.Date, revoked.RevokedAt!.Value.UtcDateTime.Date);
        Assert.Equal(ConsentBasis.Withdrawn, fixture.Consents.Items.Last().Basis);
        Assert.NotNull(fixture.Consents.Items.Last().WithdrawnAt);
        Assert.Equal("channel.identity.revoked", fixture.Audit.Events.Last().ActionName);
        await Assert.ThrowsAsync<ChannelIdentityPolicyException>(() => fixture.Service.RevokeAsync(created.LinkId, "Already revoked"));
    }

    [Fact]
    public async Task RelinkAfterRevocationRequiresNewConsentAndVerificationEvidence()
    {
        var fixture = new Fixture();
        var created = await fixture.Service.LinkAsync(new LinkChannelIdentityCommand(
            "whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant"));
        await fixture.Service.RevokeAsync(created.LinkId, "Customer withdrew permission");

        var relinked = await fixture.Service.LinkAsync(new LinkChannelIdentityCommand(
            "whatsapp", "thread-1", CustomerId, true, "consent-log-78", "Consent confirmed again"));

        Assert.Equal(created.LinkId, relinked.LinkId);
        Assert.True(relinked.IsActive);
        Assert.True(relinked.HasActiveConsent);
        Assert.Equal(ConsentBasis.ExplicitOptIn, fixture.Consents.Items.Last().Basis);
        Assert.Equal("consent-log-78", fixture.Consents.Items.Last().InteractionReference);
        Assert.Equal(new[] { "channel.identity.linked", "channel.identity.revoked", "channel.identity.linked" },
            fixture.Audit.Events.Select(auditEvent => auditEvent.ActionName));
    }

    [Fact]
    public async Task GetForThreadReflectsLatestChannelScopedConsent()
    {
        var fixture = new Fixture();
        await fixture.Service.LinkAsync(new LinkChannelIdentityCommand(
            "whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant"));
        var current = await fixture.Service.GetForThreadAsync("whatsapp", "thread-1");
        Assert.True(current!.HasActiveConsent);

        await fixture.Consents.SaveAsync(new ConsentRecord
        {
            RecordId = Guid.NewGuid(), Scope = Scope, CustomerIdentifier = CustomerId,
            Basis = ConsentBasis.Withdrawn, Purpose = ChannelIdentityConsentPurposes.ForChannel("whatsapp"),
            Channel = "whatsapp", WithdrawnAt = DateTimeOffset.UtcNow, RecordedByUserId = "staff-7",
            RecordedAt = DateTimeOffset.UtcNow, CorrelationId = "withdrawal",
        });

        var afterWithdrawal = await fixture.Service.GetForThreadAsync("whatsapp", "thread-1");
        Assert.False(afterWithdrawal!.HasActiveConsent);
    }

    [Fact]
    public async Task CustomerTimelineReturnsOnlyConsentCheckedMessagesWithoutProviderIdentifiers()
    {
        var fixture = new Fixture();
        await fixture.Service.LinkAsync(new LinkChannelIdentityCommand(
            "whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant"));

        var timeline = await fixture.Service.GetCustomerTimelineAsync(CustomerId, 1, 20);

        Assert.NotNull(timeline);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("whatsapp", entry.Channel);
        Assert.Equal("Hello", entry.Body);
        Assert.DoesNotContain(RecipientId, System.Text.Json.JsonSerializer.Serialize(timeline), StringComparison.Ordinal);
        Assert.Equal("customer.channel_timeline.viewed", fixture.Audit.Events.Last().ActionName);
        Assert.DoesNotContain(RecipientId, fixture.Audit.Events.Last().ValidationResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomerTimelineExcludesRevokedIdentityAndWithdrawnConsent()
    {
        var fixture = new Fixture();
        var linked = await fixture.Service.LinkAsync(new LinkChannelIdentityCommand(
            "whatsapp", "thread-1", CustomerId, true, "consent-log-55", "Verified with claimant"));
        await fixture.Service.RevokeAsync(linked.LinkId, "Customer withdrew permission");

        var timeline = await fixture.Service.GetCustomerTimelineAsync(CustomerId, 1, 20);

        Assert.NotNull(timeline);
        Assert.Empty(timeline.Entries);
    }

    private sealed class Fixture
    {
        private readonly TenantContextAccessor tenant = new();
        public readonly LinkRepository Links = new();
        public readonly ConsentRepository Consents = new();
        public readonly AuditWriter Audit = new();
        public readonly WhatsAppMessages WhatsApp;
        public readonly CustomerRepository Customers;
        public readonly ChannelIdentityService Service;

        public Fixture(bool customerExists = true, bool hasInboundMessage = true)
        {
            tenant.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, "staff-7", [PlatformRole.Admin.ToString()]));
            Customers = new CustomerRepository(customerExists);
            WhatsApp = new WhatsAppMessages(hasInboundMessage);
            Service = new ChannelIdentityService(
                tenant, new Permissions(), Customers, WhatsApp, new ChannelMessages(), Links,
                new WhatsAppConfiguration(), new Connections(), Consents, Audit);
        }
    }

    private sealed class Permissions : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CustomerRepository(bool exists) : ICustomerRepository
    {
        public Task<CustomerRecord?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(exists && scope.Matches(Scope) && customerId == CustomerId ? new CustomerRecord(CustomerId, Scope.TenantId, Scope.WorkspaceId, "Amina Example", null, null) : null);
        public Task<IReadOnlyCollection<CustomerRecord>> ListAsync(TenantScope scope, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<CustomerRecord>>([]);
        public Task UpsertAsync(CustomerRecord customer, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class WhatsAppMessages(bool hasInbound) : IWhatsAppMessageRepository
    {
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppMessage>>(hasInbound && scope.Matches(Scope) && threadId == "thread-1"
                ? [new WhatsAppMessage { MessageId = Guid.NewGuid(), Scope = Scope, ThreadId = threadId, BusinessPhoneNumberId = "business-phone-1", ContactWaId = RecipientId, Direction = WhatsAppMessageDirection.Inbound, ContentType = WhatsAppContentType.Text, CustomerSafeBody = "Hello", DeliveryState = WhatsAppDeliveryState.Delivered, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = "correlation" }]
                : []);
        public Task<IReadOnlyList<WhatsAppMessage>> ListForRecipientAsync(TenantScope scope, string accountId, string recipientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppMessage>>(scope.Matches(Scope) && accountId == "business-phone-1" && recipientId == RecipientId
                ? [new WhatsAppMessage { MessageId = Guid.NewGuid(), Scope = Scope, ThreadId = "thread-1", BusinessPhoneNumberId = accountId, ContactWaId = recipientId, Direction = WhatsAppMessageDirection.Inbound, ContentType = WhatsAppContentType.Text, CustomerSafeBody = "Hello", DeliveryState = WhatsAppDeliveryState.Received, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = "timeline-correlation" }]
                : []);
        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
    }

    private sealed class ChannelMessages : IChannelMessageRepository
    {
        public Task<IReadOnlyList<ChannelMessage>> ListThreadAsync(TenantScope scope, string channel, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChannelMessage>>([]);
        public Task<IReadOnlyList<ChannelMessage>> ListForExternalRecipientAsync(TenantScope scope, string channel, string accountId, string externalRecipientId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChannelMessage>>([]);
        public Task<bool> SaveInboundIfNewAsync(ChannelMessage message, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveAsync(ChannelMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ChannelMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string channel, string accountId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<ChannelMessage?>(null);
    }

    private sealed class LinkRepository : IChannelIdentityLinkRepository
    {
        public readonly List<ChannelIdentityLink> Items = [];
        public Task<ChannelIdentityLink?> FindByExternalIdentityAsync(TenantScope scope, string channel, string accountId, string externalRecipientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Scope.Matches(scope) && item.Channel == channel && item.AccountId == accountId && item.ExternalRecipientId == externalRecipientId));
        public Task<ChannelIdentityLink?> GetAsync(TenantScope scope, Guid linkId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Scope.Matches(scope) && item.LinkId == linkId));
        public Task<IReadOnlyList<ChannelIdentityLink>> ListForCustomerAsync(TenantScope scope, string customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ChannelIdentityLink>>(Items.Where(item => item.Scope.Matches(scope) && item.CustomerId == customerId).ToArray());
        public Task SaveAsync(ChannelIdentityLink link, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(item => item.LinkId == link.LinkId);
            Items.Add(link);
            return Task.CompletedTask;
        }
    }

    private sealed class WhatsAppConfiguration : IWhatsAppChannelConfigurationRepository
    {
        public Task<WhatsAppChannelConfiguration?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<WhatsAppChannelConfiguration?>(scope.Matches(Scope)
                ? Create()
                : null);
        public Task<IReadOnlyList<WhatsAppChannelConfiguration>> ListActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppChannelConfiguration>>([Create()]);
        public Task SaveAsync(WhatsAppChannelConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private static WhatsAppChannelConfiguration Create() => new()
        {
            Scope = Scope,
            PhoneNumberId = "business-phone-1",
            EncryptedAccessToken = "cipher",
            EncryptedAppSecret = "cipher",
            EncryptedVerifyToken = "cipher",
            ConfiguredByUserId = "admin",
            ConfiguredAt = DateTimeOffset.UtcNow,
            LastValidationStatus = "Success"
        };
    }

    private sealed class Connections : IIntegrationConnectionRepository
    {
        public Task<IntegrationConnection?> GetAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.FromResult<IntegrationConnection?>(null);
        public Task SaveAsync(IntegrationConnection connection, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ConsentRepository : IConsentRepository
    {
        public readonly List<ConsentRecord> Items = [];
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) { Items.Add(record); return Task.CompletedTask; }
        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) => Task.FromResult(Items.LastOrDefault(item => item.Scope.Matches(scope) && item.CustomerIdentifier == customerIdentifier && item.Purpose == purpose));
        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ConsentRecord>>(Items.Where(item => item.Scope.Matches(scope) && item.CustomerIdentifier == customerIdentifier).ToArray());
    }

    private sealed class AuditWriter : IAuditWriter
    {
        public readonly List<AuditEvent> Events = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) { Events.Add(auditEvent); return Task.CompletedTask; }
    }
}
