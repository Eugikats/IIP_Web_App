using System.Net;
using InsuranceIntelligence.Application.Webhooks;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Webhooks;
using InsuranceIntelligence.Infrastructure.Connections;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.Infrastructure.Webhooks;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Events out to an insurer's own systems: only what it subscribed to, signed, retried, and
/// never pointed at the platform's own network.</summary>
public sealed class OutboundWebhookTests : IDisposable
{
    private static readonly TenantScope Acme = TenantScope.Create("org-acme", "main");
    private static readonly TenantScope Baraka = TenantScope.Create("org-baraka", "main");
    // Relative to the real clock: the publisher stamps events with the current time, so a fixed date made these tests expire.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddMinutes(1);

    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"webhooks-{Guid.NewGuid():N}").Options);
    private readonly DataProtectionConnectionSecretProtector protector =
        new(new EphemeralDataProtectionProvider());
    private readonly FakeSender sender = new();
    private readonly EfOutboundWebhookRepository repository;

    public OutboundWebhookTests() => repository = new EfOutboundWebhookRepository(db);

    public void Dispose() => db.Dispose();

    private async Task<(OutboundWebhookSubscription Subscription, string Secret)> SubscribeAsync(
        TenantScope scope, params string[] events)
    {
        var secret = WebhookSignature.NewSecret();
        var subscription = new OutboundWebhookSubscription(
            Guid.NewGuid(), scope, "https://erp.example.com/iip", protector.Protect(secret), events.Length == 0 ? ["*"] : events, true, "admin", Now);
        await repository.SaveSubscriptionAsync(subscription);
        return (subscription, secret);
    }

    private static AuditEvent Audit(TenantScope scope, string action, string? before = "New", string? after = "UnderReview", Guid? caseId = null) =>
        new(Guid.NewGuid(), scope, caseId ?? Guid.NewGuid(), null, "officer", ActorType.User, action, Now, before, after, [], "Platform", null, "recorded", null, "reason", "c");

    [Theory]
    [InlineData("https://erp.example.com/hooks", true)]
    [InlineData("https://203.0.113.10/hooks", true)]
    [InlineData("http://erp.example.com/hooks", false)]
    [InlineData("https://localhost/hooks", false)]
    [InlineData("https://127.0.0.1/hooks", false)]
    [InlineData("https://10.0.0.5/hooks", false)]
    [InlineData("https://172.20.1.1/hooks", false)]
    [InlineData("https://192.168.1.1/hooks", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)] // the cloud metadata service
    [InlineData("https://[::1]/hooks", false)]
    [InlineData("https://[fd00::1]/hooks", false)]
    [InlineData("https://db.corp.internal/hooks", false)]
    [InlineData("https://user:pass@erp.example.com/hooks", false)]
    [InlineData("not a url", false)]
    [InlineData("", false)]
    public void OnlyPublicHttpsAddressesAreEverCalled(string url, bool allowed) =>
        Assert.Equal(allowed, WebhookUrlPolicy.TryValidate(url, out _, out _));

    [Fact]
    public void ASignatureProvesWhoSentIt_AndCoversWhenAndWhat()
    {
        var secret = "s3cret";
        var body = """{"id":"1"}""";
        var signature = WebhookSignature.Compute(secret, 1_700_000_000, body);
        var at = DateTimeOffset.FromUnixTimeSeconds(1_700_000_030);

        Assert.True(WebhookSignature.Verify(secret, 1_700_000_000, body, signature, at, TimeSpan.FromMinutes(5)));
        Assert.False(WebhookSignature.Verify("other", 1_700_000_000, body, signature, at, TimeSpan.FromMinutes(5)));
        Assert.False(WebhookSignature.Verify(secret, 1_700_000_000, body + " ", signature, at, TimeSpan.FromMinutes(5)));
        Assert.False(WebhookSignature.Verify(secret, 1_700_000_000, body, signature, at.AddMinutes(10), TimeSpan.FromMinutes(5))); // replay
        Assert.False(WebhookSignature.Verify(secret, 1_700_000_000, body, null, at, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task AClaimEvent_GoesOnlyToThisOrganisationsSubscribers_ForTheEventsTheyAskedFor_WithoutPersonalData()
    {
        var wantsStatus = await SubscribeAsync(Acme, OutboundEventTypes.ClaimStatusChanged);
        var wantsCreated = await SubscribeAsync(Acme, OutboundEventTypes.ClaimCreated);
        var wantsAll = await SubscribeAsync(Acme);
        var otherOrganisation = await SubscribeAsync(Baraka);
        var publisher = new OutboundEventPublisher(repository);

        await publisher.PublishFromAuditAsync(Audit(Acme, "case.status.updated"));
        await publisher.PublishFromAuditAsync(Audit(Acme, "organisation.created"));   // internal: never leaves
        await publisher.PublishFromAuditAsync(Audit(Acme, "customer_assistant.autoreplied"));

        var due = await repository.ListDueAsync(Now.AddMinutes(1), 50);
        Assert.Equal(
            new[] { wantsAll.Subscription.SubscriptionId, wantsStatus.Subscription.SubscriptionId }.Order(),
            due.Select(d => d.SubscriptionId).Order());
        Assert.DoesNotContain(due, d => d.SubscriptionId == wantsCreated.Subscription.SubscriptionId);
        Assert.DoesNotContain(due, d => d.SubscriptionId == otherOrganisation.Subscription.SubscriptionId);
        Assert.All(due, d => Assert.Equal(OutboundEventTypes.ClaimStatusChanged, d.EventType));
        Assert.Contains("\"status\":\"UnderReview\"", due[0].Body);
        Assert.Contains("\"claimReference\":\"CLM-", due[0].Body);
        Assert.DoesNotContain("customerName", due[0].Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", due[0].Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ADeliveredEvent_CarriesAValidSignature_AndIsNotSentAgain()
    {
        var (subscription, secret) = await SubscribeAsync(Acme);
        await new OutboundEventPublisher(repository).PublishFromAuditAsync(Audit(Acme, "case.created"));
        var dispatcher = new OutboundWebhookDispatcher(repository, protector, sender);

        Assert.Equal(1, await dispatcher.DispatchDueAsync(Now));
        Assert.Equal(0, await dispatcher.DispatchDueAsync(Now.AddMinutes(5)));

        var call = Assert.Single(sender.Calls);
        Assert.Equal("https://erp.example.com/iip", call.Url.ToString());
        var timestamp = long.Parse(call.Headers[WebhookSignature.TimestampHeader]);
        Assert.True(WebhookSignature.Verify(secret, timestamp, call.Body, call.Headers[WebhookSignature.SignatureHeader], Now, TimeSpan.FromMinutes(5)));
        Assert.Equal("claim.created", call.Headers[WebhookSignature.EventHeader]);
        var delivery = Assert.Single(await repository.ListDeliveriesAsync(Acme, subscription.SubscriptionId, 10));
        Assert.Equal(OutboundDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(Now, (await repository.GetSubscriptionAsync(Acme, subscription.SubscriptionId))!.LastDeliveredAt);
    }

    [Fact]
    public async Task AFailingReceiver_IsRetriedWithGrowingGaps_ThenGivenUpOn_AndSuccessResetsTheCount()
    {
        var (subscription, _) = await SubscribeAsync(Acme);
        await new OutboundEventPublisher(repository).PublishFromAuditAsync(Audit(Acme, "case.created"));
        var dispatcher = new OutboundWebhookDispatcher(repository, protector, sender);
        sender.Next = new WebhookSendResult(500, null);

        var clock = Now;
        for (var attempt = 1; attempt < OutboundWebhookDispatcher.MaxAttempts; attempt++)
        {
            Assert.Equal(1, await dispatcher.DispatchDueAsync(clock));
            var pending = Assert.Single(await repository.ListDeliveriesAsync(Acme, subscription.SubscriptionId, 10));
            Assert.Equal(OutboundDeliveryStatus.Pending, pending.Status);
            Assert.Equal(attempt, pending.Attempts);
            Assert.Equal(clock + OutboundWebhookDispatcher.RetryGaps[attempt - 1], pending.NextAttemptAt);
            Assert.Equal(0, await dispatcher.DispatchDueAsync(clock.AddSeconds(5))); // not before its time
            clock = pending.NextAttemptAt;
        }

        Assert.Equal(1, await dispatcher.DispatchDueAsync(clock));
        var dead = Assert.Single(await repository.ListDeliveriesAsync(Acme, subscription.SubscriptionId, 10));
        Assert.Equal(OutboundDeliveryStatus.Dead, dead.Status);
        Assert.Equal(OutboundWebhookDispatcher.MaxAttempts, dead.Attempts);
        Assert.Equal(OutboundWebhookDispatcher.MaxAttempts, (await repository.GetSubscriptionAsync(Acme, subscription.SubscriptionId))!.ConsecutiveFailures);

        // The receiver comes back: the next event is delivered and the failure count resets.
        sender.Next = new WebhookSendResult(204, null);
        await new OutboundEventPublisher(repository).PublishFromAuditAsync(Audit(Acme, "case.created"));
        await dispatcher.DispatchDueAsync(clock.AddMinutes(1));
        Assert.Equal(0, (await repository.GetSubscriptionAsync(Acme, subscription.SubscriptionId))!.ConsecutiveFailures);
    }

    [Fact]
    public async Task ADeletedOrPrivateSubscription_IsNeverCalled()
    {
        var gone = await SubscribeAsync(Acme);
        var publisher = new OutboundEventPublisher(repository);
        await publisher.PublishFromAuditAsync(Audit(Acme, "case.created"));
        await repository.DeleteSubscriptionAsync(Acme, gone.Subscription.SubscriptionId);

        var privateOne = new OutboundWebhookSubscription(
            Guid.NewGuid(), Acme, "https://10.0.0.5/steal", protector.Protect("x"), ["*"], true, "admin", Now);
        await repository.SaveSubscriptionAsync(privateOne);
        await publisher.PublishFromAuditAsync(Audit(Acme, "case.created"));
        var dispatcher = new OutboundWebhookDispatcher(repository, protector, sender);

        await dispatcher.DispatchDueAsync(Now);

        Assert.Empty(sender.Calls);
        Assert.Equal(OutboundDeliveryStatus.Dead, Assert.Single(await repository.ListDeliveriesAsync(Acme, privateOne.SubscriptionId, 10)).Status);
        Assert.Empty(await repository.ListDeliveriesAsync(Acme, gone.Subscription.SubscriptionId, 10));
    }

    [Fact]
    public async Task OneOrganisationCannotSeeOrRemoveAnothersWebhooks()
    {
        var acme = await SubscribeAsync(Acme);
        await new OutboundEventPublisher(repository).PublishFromAuditAsync(Audit(Acme, "case.created"));

        Assert.Null(await repository.GetSubscriptionAsync(Baraka, acme.Subscription.SubscriptionId));
        Assert.Empty(await repository.ListSubscriptionsAsync(Baraka));
        Assert.Empty(await repository.ListDeliveriesAsync(Baraka, acme.Subscription.SubscriptionId, 10));
        await repository.DeleteSubscriptionAsync(Baraka, acme.Subscription.SubscriptionId);
        Assert.NotNull(await repository.GetSubscriptionAsync(Acme, acme.Subscription.SubscriptionId));
    }

    [Fact]
    public async Task WritingAnAuditEntry_QueuesTheEvent_AndAProblemQueuingNeverLosesTheEntry()
    {
        await SubscribeAsync(Acme);
        var audit = new EfAuditRepository(db, null, new OutboundEventPublisher(repository));
        var caseId = Guid.NewGuid();

        await audit.WriteAsync(Audit(Acme, "case.created", caseId: caseId));

        Assert.Single(await audit.ListAsync(Acme));
        Assert.Single(await repository.ListDueAsync(Now.AddMinutes(1), 10));

        var broken = new EfAuditRepository(db, null, new OutboundEventPublisher(new BrokenRepository()));
        await broken.WriteAsync(Audit(Acme, "case.created"));
        Assert.Equal(2, (await audit.ListAsync(Acme)).Count); // the entry stands
    }

    [Fact]
    public async Task ACasesAuditTrailReadById_StaysInsideTheCallersOrganisation()
    {
        var caseId = Guid.NewGuid();
        await new EfAuditRepository(db).WriteAsync(Audit(Acme, "case.created", caseId: caseId));

        var asBaraka = new EfAuditRepository(db, new FixedTenant(Baraka));
        var asAcme = new EfAuditRepository(db, new FixedTenant(Acme));

        Assert.Empty(await asBaraka.ListForCaseAsync(caseId));
        Assert.Single(await asAcme.ListForCaseAsync(caseId));
    }

    private sealed class FakeSender : IWebhookHttpSender
    {
        public List<(Uri Url, IReadOnlyDictionary<string, string> Headers, string Body)> Calls { get; } = [];
        public WebhookSendResult Next { get; set; } = new(200, null);

        public Task<WebhookSendResult> SendAsync(Uri url, IReadOnlyDictionary<string, string> headers, string body, CancellationToken cancellationToken = default)
        {
            Calls.Add((url, headers, body));
            return Task.FromResult(Next);
        }
    }

    private sealed class FixedTenant(TenantScope scope) : InsuranceIntelligence.Application.Core.ITenantContextAccessor
    {
        public bool HasCurrent => true;
        public InsuranceIntelligence.Application.Core.TenantContext Current => new(scope.TenantId, scope.WorkspaceId, "user", []);
    }

    private sealed class BrokenRepository : IOutboundWebhookRepository
    {
        public Task SaveSubscriptionAsync(OutboundWebhookSubscription s, CancellationToken c = default) => throw new InvalidOperationException();
        public Task<OutboundWebhookSubscription?> GetSubscriptionAsync(TenantScope s, Guid id, CancellationToken c = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<OutboundWebhookSubscription>> ListSubscriptionsAsync(TenantScope s, CancellationToken c = default) => throw new InvalidOperationException();
        public Task DeleteSubscriptionAsync(TenantScope s, Guid id, CancellationToken c = default) => throw new InvalidOperationException();
        public Task SaveDeliveryAsync(OutboundDelivery d, CancellationToken c = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<OutboundDelivery>> ListDeliveriesAsync(TenantScope s, Guid id, int take, CancellationToken c = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<OutboundDelivery>> ListDueAsync(DateTimeOffset now, int take, CancellationToken c = default) => throw new InvalidOperationException();
    }
}
