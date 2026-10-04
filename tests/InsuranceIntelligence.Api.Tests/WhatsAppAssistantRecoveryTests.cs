using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>A customer message saved just before a restart must still be answered, once.</summary>
public sealed class WhatsAppAssistantRecoveryTests
{
    private static readonly TenantScope Acme = TenantScope.Create("org-acme", "main");
    private static readonly TenantScope Baraka = TenantScope.Create("org-baraka", "main");
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly Store store = new();
    private readonly RecordingAssistant assistant = new();
    private WhatsAppAssistantRecoveryService Service => new(store, store, assistant);

    [Fact]
    public async Task AMessageSavedButNeverAnswered_IsGivenToTheAssistantAgain()
    {
        store.Add(Inbound(Acme, "wamid.lost", "I had an accident", minutesAgo: 10));

        var recovered = await Service.RecoverAsync(Acme, autoReplyEnabled: true, Now);

        Assert.Equal(1, recovered);
        var call = Assert.Single(assistant.Calls);
        Assert.Equal(Acme, call.Scope);
        Assert.Equal("wamid.lost", Assert.Single(call.Messages).ProviderMessageId);
        Assert.True(call.AutoReply);
    }

    [Fact]
    public async Task NothingIsTakenThatIsStillBeingWorkedOn_OrAlreadyDealtWith()
    {
        store.Add(Inbound(Acme, "wamid.fresh", "Hello", minutesAgo: 1));                               // still within the assistant's own time limit
        store.Add(Inbound(Acme, "wamid.marked", "Marked", minutesAgo: 10, handled: true));            // the assistant said it was done
        store.Add(Inbound(Acme, "wamid.answered", "Is my claim ok?", minutesAgo: 10));                 // a reply followed, though never marked (older data)
        store.Turns.Add(Turn("thread-wamid.answered", "Is my claim ok?", minutesAgo: 9));
        store.Add(Inbound(Acme, "wamid.photo", "[Image received]", minutesAgo: 10, type: WhatsAppContentType.Image));
        store.Add(Inbound(Acme, "wamid.ancient", "Too late now", minutesAgo: 60 * 30));                 // a reply a day later would only confuse

        Assert.Equal(0, await Service.RecoverAsync(Acme, true, Now));
        Assert.Empty(assistant.Calls);
    }

    [Fact]
    public async Task AReplyThatCameBeforeTheMessage_DoesNotCountAsAnswering_AndAnotherInsurersMessagesAreNeverTouched()
    {
        store.Add(Inbound(Acme, "wamid.second", "Hi", minutesAgo: 5));
        store.Turns.Add(Turn("thread-wamid.second", "Hi", minutesAgo: 30)); // an earlier "Hi", long since answered
        store.Add(Inbound(Baraka, "wamid.baraka", "Baraka's customer", minutesAgo: 10));

        await Service.RecoverAsync(Acme, false, Now);

        var call = Assert.Single(assistant.Calls);
        Assert.Equal("wamid.second", Assert.Single(call.Messages).ProviderMessageId);
        Assert.False(call.AutoReply);
        Assert.DoesNotContain(assistant.Calls, c => c.Scope == Baraka);
    }

    [Fact]
    public async Task OnceTheAssistantFinishes_TheMessageIsMarked_SoTheSweepLeavesItAlone()
    {
        store.Add(Inbound(Acme, "wamid.lost", "Help", minutesAgo: 10));
        await Service.RecoverAsync(Acme, true, Now);
        Assert.Single(assistant.Calls);

        await store.MarkAssistantHandledAsync(Acme, "wamid.lost");

        assistant.Calls.Clear();
        Assert.Equal(0, await Service.RecoverAsync(Acme, true, Now));
        Assert.Empty(assistant.Calls);
    }

    private static WhatsAppMessage Inbound(
        TenantScope scope, string id, string body, int minutesAgo, bool handled = false, WhatsAppContentType type = WhatsAppContentType.Text) => new()
    {
        MessageId = Guid.NewGuid(),
        Scope = scope,
        ThreadId = $"thread-{id}",
        BusinessPhoneNumberId = "phone-1",
        ContactWaId = "256772118001",
        Direction = WhatsAppMessageDirection.Inbound,
        ContentType = type,
        CustomerSafeBody = body,
        ProviderMessageId = id,
        DeliveryState = WhatsAppDeliveryState.Received,
        CreatedAt = Now.AddMinutes(-minutesAgo),
        UpdatedAt = Now.AddMinutes(-minutesAgo),
        CorrelationId = "c",
        AssistantHandledAt = handled ? Now.AddMinutes(-minutesAgo + 1) : null,
    };

    private static CustomerConversationTurn Turn(string threadId, string inbound, int minutesAgo) => new()
    {
        TurnId = Guid.NewGuid(),
        Scope = Acme,
        ThreadId = threadId,
        ContactWaId = "256772118001",
        InboundMessageText = inbound,
        ClassifiedIntent = CustomerIntent.Unknown,
        Outcome = CustomerAssistantOutcome.AutoReplied,
        OccurredAt = Now.AddMinutes(-minutesAgo),
        CorrelationId = "c",
    };

    private sealed class Store : IWhatsAppMessageRepository, ICustomerConversationRepository
    {
        private readonly List<WhatsAppMessage> messages = [];
        public List<CustomerConversationTurn> Turns { get; } = [];

        public void Add(WhatsAppMessage message) => messages.Add(message);

        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
        {
            messages.RemoveAll(m => m.MessageId == message.MessageId);
            messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(messages.FirstOrDefault(m => m.Scope == scope && m.MessageId == messageId));

        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(messages.FirstOrDefault(m => m.Scope == scope && m.ProviderMessageId == providerMessageId));

        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<WhatsAppMessage?>(null);

        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppMessage>>(messages.Where(m => m.Scope == scope && m.ThreadId == threadId).ToArray());

        public Task<IReadOnlyList<WhatsAppMessage>> ListSinceAsync(TenantScope scope, DateTimeOffset since, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppMessage>>(messages.Where(m => m.Scope == scope && m.CreatedAt >= since).ToArray());

        public Task MarkAssistantHandledAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default)
        {
            var message = messages.First(m => m.Scope == scope && m.ProviderMessageId == providerMessageId);
            return SaveAsync(message with { AssistantHandledAt = Now }, cancellationToken);
        }

        Task ICustomerConversationRepository.SaveAsync(CustomerConversationTurn turn, CancellationToken cancellationToken)
        {
            Turns.Add(turn);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CustomerConversationTurn>>(Turns.Where(t => t.Scope == scope && t.ThreadId == threadId).ToArray());

        public Task<InsuranceIntelligence.Domain.Compliance.RetentionDeletionSummary> DeleteOlderThanAsync(
            TenantScope scope, DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingAssistant : ICustomerAssistantService
    {
        public List<(TenantScope Scope, IReadOnlyList<WhatsAppInboundMessage> Messages, bool AutoReply)> Calls { get; } = [];

        public Task<IReadOnlyList<Guid>> HandleInboundMessagesAsync(
            TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken = default)
        {
            Calls.Add((scope, messages, autoReplyEnabled));
            return Task.FromResult<IReadOnlyList<Guid>>([]);
        }

        public Task EscalateUnhandledAsync(
            TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, string correlationId, Exception reason, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
