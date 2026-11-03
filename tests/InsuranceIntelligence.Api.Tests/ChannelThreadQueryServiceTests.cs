using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ChannelThreadQueryServiceTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task WhatsAppThread_ExposesPendingDraftOnlyWithBothReplyPermissions(
        bool manageCommunications, bool sendCustomerUpdate, bool expectedCanReply)
    {
        const string threadId = "thread-review-draft";
        const string draft = "Review this prepared reply before sending.";
        var scope = TenantScope.Create("tenant-test", "workspace-test");
        var context = new TenantContext("tenant-test", "workspace-test", "user-test", []);
        var allowed = new List<Permission> { Permission.ViewChannels };
        if (manageCommunications) allowed.Add(Permission.ManageCustomerCommunications);
        if (sendCustomerUpdate) allowed.Add(Permission.SendCustomerUpdate);
        var permissions = new TestPermissions(allowed);
        var conversations = new TestConversations(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = "256700000000",
            InboundMessageText = "Can you confirm receipt?",
            ClassifiedIntent = CustomerIntent.GeneralQuestion,
            Outcome = CustomerAssistantOutcome.QueuedForHumanApproval,
            PendingReplyDraftText = draft,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "correlation-test"
        });
        var service = new ChannelThreadQueryService(
            new TenantContextAccessorForTest(context),
            permissions,
            new TestWhatsAppMessages(new WhatsAppMessage
            {
                MessageId = Guid.NewGuid(),
                Scope = scope,
                ThreadId = threadId,
                BusinessPhoneNumberId = "business-test",
                ContactWaId = "256700000000",
                Direction = WhatsAppMessageDirection.Inbound,
                ContentType = WhatsAppContentType.Text,
                CustomerSafeBody = "Can you confirm receipt?",
                DeliveryState = WhatsAppDeliveryState.Received,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CorrelationId = "correlation-test"
            }),
            new TestChannelMessages(),
            new TestConnections(),
            new TestConsents(),
            new TestEmails(),
            new TestAuditWriter(),
            conversations);

        var result = await service.GetAsync("whatsapp", threadId);

        Assert.NotNull(result);
        Assert.Equal(expectedCanReply, result.CanReplyInInbox);
        Assert.Equal(expectedCanReply ? draft : null, result.PendingReplyDraft);
    }

    private sealed class TenantContextAccessorForTest(TenantContext current) : ITenantContextAccessor
    {
        public TenantContext Current { get; } = current;
        public bool HasCurrent => true;
    }

    private sealed class TestPermissions(IReadOnlyCollection<Permission> allowed) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed.Contains(permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            allowed.Contains(permission) ? Task.CompletedTask : Task.FromException(new PermissionDeniedException(permission));
    }

    private sealed class TestWhatsAppMessages(WhatsAppMessage message) : IWhatsAppMessageRepository
    {
        public Task SaveAsync(WhatsAppMessage item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WhatsAppMessage>>([message]);
    }

    private sealed class TestChannelMessages : IChannelMessageRepository
    {
        public Task<bool> SaveInboundIfNewAsync(ChannelMessage message, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveAsync(ChannelMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ChannelMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string channel, string accountId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<ChannelMessage?>(null);
        public Task<IReadOnlyList<ChannelMessage>> ListThreadAsync(TenantScope scope, string channel, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChannelMessage>>([]);
    }

    private sealed class TestConnections : IIntegrationConnectionRepository
    {
        public Task<IntegrationConnection?> GetAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.FromResult<IntegrationConnection?>(null);
        public Task SaveAsync(IntegrationConnection connection, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestConsents : IConsentRepository
    {
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) => Task.FromResult<ConsentRecord?>(null);
        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ConsentRecord>>([]);
    }

    private sealed class TestEmails : IInboundEmailRepository
    {
        public Task SaveAsync(InboundEmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<InboundEmailMessage?> GetAsync(Guid messageId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<InboundEmailMessage?>(null);
        public Task<IReadOnlyCollection<InboundEmailMessage>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<InboundEmailMessage>>([]);
    }

    private sealed class TestAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestConversations(CustomerConversationTurn turn) : ICustomerConversationRepository
    {
        public Task SaveAsync(CustomerConversationTurn item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<CustomerConversationTurn>>([turn]);
        public Task<InsuranceIntelligence.Domain.Compliance.RetentionDeletionSummary> DeleteOlderThanAsync(TenantScope scope, DateTimeOffset cutoff, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
