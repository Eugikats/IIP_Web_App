using System.Collections.Concurrent;
using System.Text.Json;
using InsuranceIntelligence.Api.Academy;
using InsuranceIntelligence.Api.Academy.Payments;
using InsuranceIntelligence.Domain.Academy;
using Microsoft.Extensions.Configuration;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Selling a course: the server sets the price, only the provider's word counts, the amount must match, and a course opens once.</summary>
public sealed class AcademyPaymentTests
{
    private static readonly AcademyContentProvider Content = new();

    private sealed class MemoryStore : IAcademyRecordStore
    {
        private readonly ConcurrentDictionary<(string, string, string), AcademyRecord> rows = new();

        public Task<AcademyRecord?> GetAsync(string kind, string owner, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.TryGetValue((kind, owner, key), out var row) ? row : null);

        public Task SaveAsync(string kind, string owner, string key, string json, CancellationToken cancellationToken = default)
        {
            rows[(kind, owner, key)] = new AcademyRecord(kind, owner, key, json, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AcademyRecord>> ListByOwnerAsync(string kind, string owner, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AcademyRecord>>(rows.Values.Where(row => row.Kind == kind && row.Owner == owner).ToList());

        public Task<IReadOnlyList<AcademyRecord>> ListByKindAsync(string kind, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AcademyRecord>>(rows.Values.Where(row => row.Kind == kind).Take(take).ToList());

        public Task DeleteAsync(string kind, string owner, string key, CancellationToken cancellationToken = default)
        {
            rows.TryRemove((kind, owner, key), out _);
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(string kind, CancellationToken cancellationToken = default) => Task.FromResult(rows.Values.Count(row => row.Kind == kind));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Prices : IAcademyPriceSource
    {
        public decimal Amount { get; set; } = 85m;

        public Task<IReadOnlyDictionary<string, decimal>> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, decimal>>(AcademyPricing.CourseIds.ToDictionary(id => id, _ => Amount));
    }

    private sealed class Gateway : IPaymentGateway
    {
        public string Name => "test";

        public bool Configured { get; set; } = true;

        public List<GatewayCheckout> Checkouts { get; } = [];

        /// <summary>What the provider will say about a reference. Absent means it knows no such payment.</summary>
        public Dictionary<string, GatewayPayment> Known { get; } = [];

        public bool ThrowOnCreate { get; set; }

        public Task<string> CreateCheckoutAsync(GatewayCheckout checkout, CancellationToken cancellationToken)
        {
            if (ThrowOnCreate) throw new AcademyRuleException(502, "down", "gateway_error");
            Checkouts.Add(checkout);
            return Task.FromResult("https://pay.example/" + checkout.Reference);
        }

        public Task<GatewayPayment?> VerifyAsync(string? transactionId, string reference, CancellationToken cancellationToken) =>
            Task.FromResult(Known.TryGetValue(reference, out var payment) ? payment : null);

        public bool WebhookAuthentic(string? signatureHeader) => signatureHeader == "right-hash";

        public void Pays(OrderView order, decimal? amount = null, string? currency = null, string status = "successful") =>
            Known[order.Reference] = new GatewayPayment(status == "successful", status, order.Reference, "777", amount ?? order.Amount, currency ?? order.Currency);
    }

    private sealed record OrderView(string Reference, decimal Amount, string Currency);

    private sealed record Rig(AcademyPaymentService Payments, AcademyLearnerService Learners, AcademySettingsService Settings, Gateway Gateway, Prices Prices, Clock Clock, AcademyLearner Learner);

    private static Rig NewRig()
    {
        var store = new MemoryStore();
        var clock = new Clock();
        var learners = new AcademyLearnerService(store, Content, clock);
        var configuration = new ConfigurationBuilder().Build();
        var settings = new AcademySettingsService(store, configuration);
        var gateway = new Gateway();
        var prices = new Prices();
        return new Rig(new AcademyPaymentService(store, Content, prices, learners, settings, gateway, configuration, clock), learners, settings, gateway, prices, clock,
            new AcademyLearner("u1", "Aisha Namutebi", "aisha@example.test", false, false));
    }

    private static async Task<AcademyRuleException> Refused(Func<Task> action) => await Assert.ThrowsAsync<AcademyRuleException>(action);

    private static OrderView LastOrder(Rig rig)
    {
        var checkout = rig.Gateway.Checkouts[^1];
        return new OrderView(checkout.Reference, checkout.Amount, checkout.Currency);
    }

    private static async Task<List<JsonElement>> OrdersOf(Rig rig) =>
        JsonSerializer.SerializeToElement(await rig.Payments.OrdersAsync(default)).GetProperty("orders").EnumerateArray().ToList();

    private const string Course = "ai-agentic-insurance";

    [Fact]
    public async Task TheOffer_IsWorkedOutOnTheServer_InDollarsAndInRoundedShillings()
    {
        var rig = NewRig();
        var offer = await rig.Payments.OfferAsync("u1", Course, default);

        Assert.Equal(85m, offer.PriceUsd);
        Assert.False(offer.Free);
        var card = offer.Choices.Single(choice => choice.Method == "card");
        Assert.Equal(("USD", 85m), (card.Currency, card.Amount));
        var money = offer.Choices.Single(choice => choice.Method == "mobile_money");
        Assert.Equal(("UGX", 350_000m), (money.Currency, money.Amount));

        await rig.Settings.SavePaymentsAsync(3700m, "UGX", null, default);
        offer = await rig.Payments.OfferAsync("u1", Course, default);
        Assert.Equal(("UGX", 315_000m), (offer.Choices[0].Currency, offer.Choices[0].Amount));
    }

    [Fact]
    public async Task AFreeCourse_NeedsNoPayment_AndHasNoCheckout()
    {
        var rig = NewRig();
        rig.Prices.Amount = 0m;

        Assert.False(await rig.Payments.RequiresPaymentAsync(Course, default));
        Assert.Equal("free_course", (await Refused(() => rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default))).Code);
    }

    [Fact]
    public async Task WithNoProviderSetUp_EnrolmentStaysOpen_AndCheckoutSaysSo()
    {
        var rig = NewRig();
        rig.Gateway.Configured = false;

        Assert.False(await rig.Payments.RequiresPaymentAsync(Course, default));
        Assert.Equal("payments_not_configured", (await Refused(() => rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default))).Code);
    }

    [Fact]
    public async Task WithAProvider_APricedCourseNeedsPayment()
    {
        var rig = NewRig();
        Assert.True(await rig.Payments.RequiresPaymentAsync(Course, default));
    }

    [Fact]
    public async Task Checkout_MakesAPendingOrderWithOurOwnReference_AndTheServersAmount()
    {
        var rig = NewRig();
        var result = await rig.Payments.CheckoutAsync(rig.Learner, Course, "mobile_money", default);

        Assert.StartsWith("IIPA-", result.Reference);
        var sent = Assert.Single(rig.Gateway.Checkouts);
        Assert.Equal((result.Reference, 350_000m, "UGX", "mobile_money"), (sent.Reference, sent.Amount, sent.Currency, sent.Method));
        Assert.Equal("pending", (await OrdersOf(rig))[0].GetProperty("status").GetString());
        Assert.False(await rig.Learners.IsEnrolledAsync("u1", Course, default));
        Assert.Equal(400, (await Refused(() => rig.Payments.CheckoutAsync(rig.Learner, Course, "bitcoin", default))).Status);
    }

    [Fact]
    public async Task APaymentTheProviderConfirms_EnrolsOnce_AndTellsTheLearner()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var order = LastOrder(rig);
        rig.Gateway.Pays(order);

        var first = await rig.Payments.ConfirmAsync(rig.Learner, order.Reference, "777", default);
        var again = await rig.Payments.ConfirmAsync(rig.Learner, order.Reference, "777", default);

        Assert.Equal("paid", first.Status);
        Assert.Equal("paid", again.Status);
        Assert.True(await rig.Learners.IsEnrolledAsync("u1", Course, default));
        var state = JsonSerializer.SerializeToElement(await rig.Learners.StateAsync(rig.Learner, default));
        var notes = state.GetProperty("notifications").EnumerateArray().Count(item => item.GetProperty("text").GetString()!.StartsWith("Payment received"));
        Assert.Equal(1, notes);
    }

    [Fact]
    public async Task UntilTheProviderConfirms_NobodyIsEnrolled_EvenIfTheBrowserSaysItPaid()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);

        var result = await rig.Payments.ConfirmAsync(rig.Learner, LastOrder(rig).Reference, "999", default);

        Assert.Equal("pending", result.Status);
        Assert.False(await rig.Learners.IsEnrolledAsync("u1", Course, default));
    }

    [Theory]
    [InlineData(10, "USD")]
    [InlineData(84.99, "USD")]
    [InlineData(85, "UGX")]
    public async Task ALessOrDifferentPayment_IsFlaggedForAPerson_AndEnrolsNobody(decimal paid, string currency)
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var order = LastOrder(rig);
        rig.Gateway.Pays(order, paid, currency);

        var result = await rig.Payments.ConfirmAsync(rig.Learner, order.Reference, "777", default);

        Assert.Equal("mismatch", result.Status);
        Assert.False(await rig.Learners.IsEnrolledAsync("u1", Course, default));
    }

    [Fact]
    public async Task APaymentTheProviderReportsFailed_EnrolsNobody()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var order = LastOrder(rig);
        rig.Gateway.Pays(order, status: "failed");

        Assert.Equal("failed", (await rig.Payments.ConfirmAsync(rig.Learner, order.Reference, "777", default)).Status);
        Assert.False(await rig.Learners.IsEnrolledAsync("u1", Course, default));
    }

    [Fact]
    public async Task AVerifiedPaymentForAnotherReference_CannotEnrolTheOrderOwner()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var order = LastOrder(rig);
        rig.Gateway.Pays(order);
        rig.Gateway.Known[order.Reference] = rig.Gateway.Known[order.Reference] with { Reference = "IIPA-another-order" };

        Assert.Equal("mismatch", (await rig.Payments.ConfirmAsync(rig.Learner, order.Reference, "777", default)).Status);
        Assert.False(await rig.Learners.IsEnrolledAsync("u1", Course, default));
    }

    [Fact]
    public async Task OnlyTheOwnerOfAnOrder_CanConfirmIt()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var order = LastOrder(rig);
        rig.Gateway.Pays(order);

        var other = new AcademyLearner("u2", "Ben", null, false, false);
        Assert.Equal(404, (await Refused(() => rig.Payments.ConfirmAsync(other, order.Reference, "777", default))).Status);
        Assert.Equal(404, (await Refused(() => rig.Payments.ConfirmAsync(rig.Learner, "IIPA-NOPE", "777", default))).Status);
        Assert.False(await rig.Learners.IsEnrolledAsync("u2", Course, default));
    }

    [Fact]
    public async Task TheWebhook_NeedsTheSecret_IgnoresOtherAppsPayments_AndNeverBelievesItsOwnBody()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var order = LastOrder(rig);
        var body = "{\"event\":\"charge.completed\",\"data\":{\"id\":777,\"tx_ref\":\"" + order.Reference + "\",\"status\":\"successful\",\"amount\":85}}";

        Assert.Equal("rejected", await rig.Payments.WebhookAsync("wrong", body, default));
        Assert.Equal("rejected", await rig.Payments.WebhookAsync(null, body, default));
        Assert.Equal("ignored", await rig.Payments.WebhookAsync("right-hash", "{\"data\":{\"tx_ref\":\"OTIC-2026-1\"}}", default));
        Assert.Equal("ignored", await rig.Payments.WebhookAsync("right-hash", "not json", default));

        // A signed webhook that says "successful" does nothing while the provider has no such payment.
        Assert.Equal("ok", await rig.Payments.WebhookAsync("right-hash", body, default));
        Assert.False(await rig.Learners.IsEnrolledAsync("u1", Course, default));

        rig.Gateway.Pays(order);
        Assert.Equal("ok", await rig.Payments.WebhookAsync("right-hash", body, default));
        Assert.True(await rig.Learners.IsEnrolledAsync("u1", Course, default));
    }

    [Fact]
    public async Task TheBackgroundCheck_EnrolsSomeoneWhoPaidAndClosedTheTab_AndClosesOrdersThatNeverPaid()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var paid = LastOrder(rig);
        var ben = new AcademyLearner("u2", "Ben", "ben@example.test", false, false);
        await rig.Payments.CheckoutAsync(ben, Course, "card", default);
        var abandoned = LastOrder(rig);
        rig.Gateway.Pays(paid);

        Assert.Equal(0, await rig.Payments.ReconcileAsync(default));
        rig.Clock.Now = rig.Clock.Now.AddMinutes(10);
        Assert.Equal(1, await rig.Payments.ReconcileAsync(default));
        Assert.True(await rig.Learners.IsEnrolledAsync("u1", Course, default));
        Assert.False(await rig.Learners.IsEnrolledAsync("u2", Course, default));

        rig.Clock.Now = rig.Clock.Now.AddDays(4);
        await rig.Payments.ReconcileAsync(default);
        var orders = await OrdersOf(rig);
        Assert.Equal("failed", orders.Single(item => item.GetProperty("reference").GetString() == abandoned.Reference).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ARefund_IsRecorded_AndCanTakeTheCourseAway_ButNotATwiceRefund()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        var order = LastOrder(rig);
        rig.Gateway.Pays(order);
        await rig.Payments.ConfirmAsync(rig.Learner, order.Reference, "777", default);

        await rig.Payments.MarkRefundedAsync(order.Reference, revokeAccess: true, default);

        Assert.False(await rig.Learners.IsEnrolledAsync("u1", Course, default));
        Assert.Equal(409, (await Refused(() => rig.Payments.MarkRefundedAsync(order.Reference, false, default))).Status);
    }

    [Fact]
    public async Task AlreadyEnrolled_CannotBuyAgain_AndAProviderOutageFailsTheOrderCleanly()
    {
        var rig = NewRig();
        rig.Gateway.ThrowOnCreate = true;
        Assert.Equal("gateway_error", (await Refused(() => rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default))).Code);
        Assert.Equal("failed", (await OrdersOf(rig))[0].GetProperty("status").GetString());

        rig.Gateway.ThrowOnCreate = false;
        await rig.Learners.EnrolAsync("u1", Course, "operator", null, default);
        Assert.Equal("already_enrolled", (await Refused(() => rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default))).Code);
    }

    [Fact]
    public async Task TaxIsSplitOutOfThePrice_OnlyWhenARateIsSet()
    {
        var rig = NewRig();
        await rig.Payments.CheckoutAsync(rig.Learner, Course, "card", default);
        Assert.Equal(0m, (await OrdersOf(rig))[0].GetProperty("taxAmount").GetDecimal());

        var taxed = NewRig();
        await taxed.Settings.SavePaymentsAsync(null, null, 18m, default);
        await taxed.Payments.CheckoutAsync(taxed.Learner, Course, "card", default);
        Assert.Equal(12.97m, (await OrdersOf(taxed))[0].GetProperty("taxAmount").GetDecimal());
    }

    [Fact]
    public async Task SettingsAreValidated_AndZenithDefaultsAreTenAndAMillion()
    {
        var rig = NewRig();
        Assert.Equal(400, (await Refused(() => rig.Settings.SavePaymentsAsync(5m, null, null, default))).Status);
        Assert.Equal(400, (await Refused(() => rig.Settings.SavePaymentsAsync(null, "EUR", null, default))).Status);
        Assert.Equal(400, (await Refused(() => rig.Settings.SavePaymentsAsync(null, null, 80m, default))).Status);
        Assert.Equal(400, (await Refused(() => rig.Settings.SaveZenithAsync(0, null, default))).Status);
        Assert.Equal(400, (await Refused(() => rig.Settings.SaveZenithAsync(null, 5, default))).Status);

        var zenith = await rig.Settings.ZenithAsync(default);
        Assert.Equal((10, 1_000_000L), (zenith.DailyLimit, zenith.MonthlyTokens));
        await rig.Settings.SaveZenithAsync(5, 500_000, default);
        var saved = await rig.Settings.ZenithAsync(default);
        Assert.Equal((5, 500_000L), (saved.DailyLimit, saved.MonthlyTokens));
    }
}
