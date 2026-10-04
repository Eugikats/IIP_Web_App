using System.Text.Json;
using InsuranceIntelligence.Application.Marketplace;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Marketplace;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Infrastructure.Marketplace;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class MarketplaceFulfillmentTests : IDisposable
{
    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"market-{Guid.NewGuid():N}").Options);
    private readonly FakeMarketplace microsoft = new();
    private readonly MarketplaceFulfillmentService service;
    private readonly EfOrganisationRepository organisations;
    private readonly EfPermissionReadRepository permissions;

    public MarketplaceFulfillmentTests()
    {
        organisations = new EfOrganisationRepository(db);
        var audit = new InMemoryAuditRepository();
        var organisationService = new OrganisationService(organisations, new EfTenantMembershipWriter(db), audit, new InMemoryCaseRepository());
        service = new MarketplaceFulfillmentService(
            microsoft, organisations, organisationService, audit, MarketplacePlanMap.Parse("insurer-starter=trial; insurer-standard=standard"));
        permissions = new EfPermissionReadRepository(db);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task APurchase_BecomesAnOrganisationOnTheRightPlan_AndMicrosoftIsToldItIsSetUp()
    {
        microsoft.Subscription = Sub("sub-1", "insurer-standard", MarketplaceSubscriptionInfo.PendingFulfillmentStart);

        var organisation = await service.ActivateAsync("buyer", "Acme Assurance", "token-1", "c");

        Assert.Equal("standard", organisation.PlanId);
        Assert.Equal("sub-1", organisation.MarketplaceSubscriptionId);
        Assert.Equal(("sub-1", "insurer-standard", 5), microsoft.Activated.Single());
        Assert.Contains("Admin", (await permissions.GetEffectiveMembershipAsync("buyer", null, null))!.Roles);
    }

    [Fact]
    public async Task ActivatingTwice_DoesNotMakeASecondOrganisation_AndAnotherPersonCannotTakeOver()
    {
        microsoft.Subscription = Sub("sub-1", "insurer-starter", MarketplaceSubscriptionInfo.PendingFulfillmentStart);
        var first = await service.ActivateAsync("buyer", "Acme Assurance", "token-1", "c");

        var again = await service.ActivateAsync("buyer", "Acme Assurance", "token-1", "c");
        var intruder = await Assert.ThrowsAsync<OrganisationException>(() => service.ActivateAsync("intruder", "Mine Now", "token-1", "c"));

        Assert.Equal(first.OrganisationId, again.OrganisationId);
        Assert.Equal("forbidden", intruder.Code);
        Assert.Single(db.Organisations);
    }

    [Fact]
    public async Task IfMicrosoftRefusesTheActivation_TheBuyerCanSimplyTryAgain()
    {
        microsoft.Subscription = Sub("sub-1", "insurer-starter", MarketplaceSubscriptionInfo.PendingFulfillmentStart);
        microsoft.AcceptActivation = false;
        var failed = await Assert.ThrowsAsync<OrganisationException>(() => service.ActivateAsync("buyer", "Acme Assurance", "token-1", "c"));

        microsoft.AcceptActivation = true;
        var organisation = await service.ActivateAsync("buyer", "Acme Assurance", "token-1", "c");

        Assert.Equal("upstream", failed.Code);
        Assert.Equal("sub-1", organisation.MarketplaceSubscriptionId);
        Assert.Single(db.Organisations);
        Assert.Single(microsoft.Activated);
    }

    [Fact]
    public async Task ASuspendedOrCancelledSubscription_ClosesTheDoor_AndReinstatingOpensItAgain()
    {
        microsoft.Subscription = Sub("sub-1", "insurer-starter", MarketplaceSubscriptionInfo.PendingFulfillmentStart);
        var organisation = await service.ActivateAsync("buyer", "Acme Assurance", "token-1", "c");
        Assert.NotNull(await permissions.GetEffectiveMembershipAsync("buyer", null, null));

        microsoft.Subscription = Sub("sub-1", "insurer-starter", MarketplaceSubscriptionInfo.Suspended);
        Assert.Equal("updated", await service.SyncAsync("sub-1", "c"));
        Assert.Null(await new EfPermissionReadRepository(db).GetEffectiveMembershipAsync("buyer", null, null));

        microsoft.Subscription = Sub("sub-1", "insurer-starter", MarketplaceSubscriptionInfo.Subscribed);
        Assert.Equal("updated", await service.SyncAsync("sub-1", "c"));
        Assert.NotNull(await new EfPermissionReadRepository(db).GetEffectiveMembershipAsync("buyer", null, null));

        microsoft.Subscription = Sub("sub-1", "insurer-starter", MarketplaceSubscriptionInfo.Unsubscribed);
        await service.SyncAsync("sub-1", "c");
        Assert.Equal("Cancelled", (await organisations.GetAsync(organisation.OrganisationId))!.Status);
    }

    [Fact]
    public async Task APlanChangeAtMicrosoft_ChangesTheLimitsHere_AndAForgedWebhookChangesNothing()
    {
        microsoft.Subscription = Sub("sub-1", "insurer-starter", MarketplaceSubscriptionInfo.PendingFulfillmentStart);
        var organisation = await service.ActivateAsync("buyer", "Acme Assurance", "token-1", "c");

        // The body of a webhook is never believed; only Microsoft's own answer is.
        Assert.Equal("unchanged", await service.SyncAsync("sub-1", "c"));
        Assert.Equal("unknown", await service.SyncAsync("not-a-subscription", "c"));

        microsoft.Subscription = Sub("sub-1", "insurer-standard", MarketplaceSubscriptionInfo.Subscribed);
        await service.SyncAsync("sub-1", "c");
        Assert.Equal("standard", (await organisations.GetAsync(organisation.OrganisationId))!.PlanId);
    }

    [Fact]
    public async Task AnUnknownPlanGetsTheSmallest_AndWithoutAPublisherAccountNothingPretendsToWork()
    {
        Assert.Equal("trial", MarketplacePlanMap.Parse("a=standard").PlanFor("something-new"));
        Assert.Equal("standard", MarketplacePlanMap.Parse("a=standard").PlanFor("a"));

        var unconnected = new MarketplaceFulfillmentService(
            new NotConfiguredMarketplaceFulfillmentClient(), organisations,
            new OrganisationService(organisations, new EfTenantMembershipWriter(db), new InMemoryAuditRepository(), new InMemoryCaseRepository()),
            new InMemoryAuditRepository(), MarketplacePlanMap.Parse(null));
        var error = await Assert.ThrowsAsync<OrganisationException>(() => unconnected.ResolveAsync("token"));
        Assert.Equal("not_configured", error.Code);
    }

    [Fact]
    public void MicrosoftsTwoReplyShapes_AreBothUnderstood()
    {
        var resolved = MicrosoftMarketplaceFulfillmentClient.Read(JsonDocument.Parse("""
            {"id":"sub-9","subscriptionName":"Acme","offerId":"iip","planId":"insurer-standard","quantity":3,
             "subscription":{"id":"sub-9","saasSubscriptionStatus":"PendingFulfillmentStart","purchaser":{"emailId":"buyer@acme.example"}}}
            """).RootElement);
        var plain = MicrosoftMarketplaceFulfillmentClient.Read(JsonDocument.Parse("""
            {"id":"sub-9","offerId":"iip","planId":"insurer-standard","quantity":1,"saasSubscriptionStatus":"Subscribed","name":"Acme"}
            """).RootElement);

        Assert.Equal(("sub-9", "insurer-standard", 3, "PendingFulfillmentStart", "buyer@acme.example"),
            (resolved!.SubscriptionId, resolved.PlanId, resolved.Quantity, resolved.Status, resolved.PurchaserEmail));
        Assert.Equal("Subscribed", plain!.Status);
        Assert.Null(MicrosoftMarketplaceFulfillmentClient.Read(JsonDocument.Parse("{}").RootElement));
    }

    private static MarketplaceSubscriptionInfo Sub(string id, string plan, string status) =>
        new(id, "iip-saas", plan, 5, status, "buyer@acme.example", "Acme");

    private sealed class FakeMarketplace : IMarketplaceFulfillmentClient
    {
        public MarketplaceSubscriptionInfo? Subscription { get; set; }
        public bool AcceptActivation { get; set; } = true;
        public List<(string SubscriptionId, string PlanId, int Quantity)> Activated { get; } = [];
        public bool IsConfigured => true;

        public Task<MarketplaceSubscriptionInfo?> ResolveAsync(string purchaseToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(Subscription);

        public Task<MarketplaceSubscriptionInfo?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Subscription is { } s && s.SubscriptionId == subscriptionId ? s : null);

        public Task<bool> ActivateAsync(string subscriptionId, string planId, int quantity, CancellationToken cancellationToken = default)
        {
            if (!AcceptActivation)
            {
                return Task.FromResult(false);
            }

            Activated.Add((subscriptionId, planId, quantity));
            Subscription = Subscription! with { Status = MarketplaceSubscriptionInfo.Subscribed };
            return Task.FromResult(true);
        }
    }
}
