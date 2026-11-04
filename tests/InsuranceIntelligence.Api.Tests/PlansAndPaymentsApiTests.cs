using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Api.Local.Mock;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Build-loop item 32: plans with monthly allowances that are enforced, buying a plan with mobile money or a card, callbacks that are only
/// ever a nudge, renewal and lapse, receipts and reconciliation. Everything runs on the pretend payment operator.
/// </summary>
public sealed class PlansAndPaymentsApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string AdminEmail = "billing.admin@example.test";
    private const string Phone = "256700123456";

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private const string SmallCatalogue = """
        [{"key":"free","name":"Free","rank":0,"prices":{"UGX":0,"USD":0},"allowances":{"claimsPerMonth":2,"documentsPerMonth":5,"aiTokensPerMonth":500,"storageMb":10,"seats":2}},
         {"key":"agent","name":"Agent","rank":1,"prices":{"UGX":75000,"USD":20},"allowances":{"claimsPerMonth":4,"documentsPerMonth":50,"aiTokensPerMonth":100000,"storageMb":100,"seats":3}},
         {"key":"team","name":"Team","rank":2,"prices":{"UGX":370000,"USD":99},"allowances":{"claimsPerMonth":9,"documentsPerMonth":500,"aiTokensPerMonth":1000000,"storageMb":1000,"seats":15}},
         {"key":"enterprise","name":"Enterprise","rank":3,"selfServe":false,"prices":{},"allowances":{}}]
        """;

    private WebApplicationFactory<Program> With(ManualTime? time = null, string? catalogue = SmallCatalogue, string payments = "Mock") =>
        factory.WithWebHostBuilder(builder =>
        {
            if (catalogue is not null)
            {
                builder.UseSetting("INSURANCEINTELLIGENCE_PLAN_CATALOGUE", catalogue);
            }

            builder.UseSetting("INSURANCEINTELLIGENCE_PAYMENT_PROVIDER", payments);
            builder.ConfigureServices(services =>
            {
                if (time is not null)
                {
                    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time));
                }
            });
        });

    private static async Task<HttpClient> WorkspaceAsync(WebApplicationFactory<Program> host, string tenant, string plan = "free", string? email = AdminEmail)
    {
        var scope = TenantScope.Create(tenant, "workspace-main");
        using var services = host.Services.CreateScope();
        var users = services.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        await users.SaveAsync(new InsuranceUserAccount { UserId = "test-admin", Scope = scope, Name = "Test Admin", Email = email ?? "none@example.test", Role = PlatformRole.Admin });
        if (plan != "none")
        {
            var store = services.ServiceProvider.GetRequiredService<ISubscriptionStore>();
            var now = DateTimeOffset.UtcNow;
            await store.SaveAsync(new Subscription { Scope = scope, PlanKey = plan, StartedAt = now, UpdatedAt = now, BillingEmail = email, PeriodEnd = plan == "free" ? null : now.AddDays(20) });
        }

        return host.CreateScopedClient(tenant);
    }

    private static async Task<JsonElement> PlanAsync(HttpClient admin) =>
        await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/plan"));

    private static async Task<JsonElement> StartAsync(HttpClient admin, string plan = "agent", string method = "mtn-momo", string currency = "UGX", string phone = Phone)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/billing/checkout", new { planKey = plan, method, currency, payerPhone = phone });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await MockedApiFactory.ReadJsonAsync(response);
    }

    private static MockPaymentEntry Entry(WebApplicationFactory<Program> host, JsonElement payment) =>
        host.Services.GetRequiredService<MockPaymentLedger>().All().Single(entry => entry.Reference == $"mock-{Guid.Parse(payment.GetProperty("paymentIntentId").GetString()!):N}");

    private static async Task SettleAsync(HttpClient admin, MockPaymentEntry entry, string outcome = "succeeded", decimal? amount = null)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/dev/mock-payments/{entry.Reference}/settle", new { outcome, amount });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> PaymentAsync(HttpClient admin, JsonElement payment) =>
        await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/admin/billing/payments/{payment.GetProperty("paymentIntentId").GetString()}"));

    private static string CallbackPath(MockPaymentEntry entry) => new Uri(entry.CallbackUrl).AbsolutePath;

    private static async Task<JsonElement[]> OutboxAsync(HttpClient admin) =>
        (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/dev/mock-outbox"))).EnumerateArray().ToArray();

    // ---- plans on the page

    [Fact]
    public async Task APlanWorkspaceSeesItsPlanItsAllowancesAndWhatItCanBuy()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        var plan = await PlanAsync(admin);

        Assert.Equal("free", plan.GetProperty("current").GetProperty("key").GetString());
        Assert.Equal(2, plan.GetProperty("current").GetProperty("allowances").GetProperty("claimsPerMonth").GetInt32());
        Assert.Equal(["free", "agent", "team", "enterprise"], plan.GetProperty("plans").EnumerateArray().Select(item => item.GetProperty("key").GetString()));
        Assert.Equal(["mtn-momo", "airtel-money", "card"], plan.GetProperty("paymentMethods").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["UGX", "USD"], plan.GetProperty("currencies").EnumerateArray().Select(item => item.GetString()));
        var billing = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing"));
        Assert.Equal("IIP Operations, Free", billing.GetProperty("packageName").GetString());
        Assert.Equal(2, billing.GetProperty("seatLimit").GetInt32());
        Assert.Equal(2, billing.GetProperty("meters").EnumerateArray().Single(meter => meter.GetProperty("key").GetString() == "claims").GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task AWorkspaceWithNoPlanKeepsItsOwnLimitsAndIsNeverBlocked()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant, plan: "none");

        var plan = await PlanAsync(admin);
        for (var i = 0; i < 4; i++)
        {
            await TestData.CreateCaseAsync(admin, tenant);
        }

        Assert.Equal("standard", plan.GetProperty("current").GetProperty("key").GetString());
        Assert.Empty(plan.GetProperty("prompts").EnumerateArray());
        var billing = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing"));
        Assert.Equal(TenantSettings.DefaultSeatLimit, billing.GetProperty("seatLimit").GetInt32());
    }

    // ---- allowances are enforced

    [Fact]
    public async Task TheClaimAfterTheMonthlyAllowanceIsRefusedWithAPlainPaymentRequiredAnswer()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        await TestData.CreateCaseAsync(admin, tenant);
        await TestData.CreateCaseAsync(admin, tenant);

        var third = await admin.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Third Customer", customerEmail = "third@example.test", customerPhone = "+256700000003", policyNumber = "POL-THIRD",
            productLine = "Motor", claimType = "Accident", channel = "Phone", incidentDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"), incidentDescription = "Collision.",
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, third.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(third);
        Assert.Equal("plan_limit", body.GetProperty("code").GetString());
        Assert.Equal("claims", body.GetProperty("meter").GetString());
        Assert.Equal(2, body.GetProperty("limit").GetInt32());
        Assert.Equal("agent", body.GetProperty("upgradeToPlanKey").GetString());
        Assert.Contains("Free plan", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task OneWorkspacesUsedUpAllowanceNeverStopsAnother()
    {
        using var host = With();
        var busy = MockedApiFactory.NewTenant();
        var quiet = MockedApiFactory.NewTenant();
        using var busyAdmin = await WorkspaceAsync(host, busy);
        using var quietAdmin = await WorkspaceAsync(host, quiet);
        await TestData.CreateCaseAsync(busyAdmin, busy);
        await TestData.CreateCaseAsync(busyAdmin, busy);

        var claim = await TestData.CreateCaseAsync(quietAdmin, quiet);

        Assert.NotEqual(Guid.Empty, claim.CaseId);
    }

    [Fact]
    public async Task PromptsAppearAtEightyPercentAndWhenTheAllowanceIsUsedUp()
    {
        using var host = With(catalogue: SmallCatalogue.Replace("\"claimsPerMonth\":2", "\"claimsPerMonth\":5"));
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        for (var i = 0; i < 4; i++)
        {
            await TestData.CreateCaseAsync(admin, tenant);
        }

        var warning = (await PlanAsync(admin)).GetProperty("prompts").EnumerateArray().Single(prompt => prompt.GetProperty("meter").GetString() == "claims");
        await TestData.CreateCaseAsync(admin, tenant);
        var limit = (await PlanAsync(admin)).GetProperty("prompts").EnumerateArray().Single(prompt => prompt.GetProperty("meter").GetString() == "claims");

        Assert.Equal("warning", warning.GetProperty("level").GetString());
        Assert.Contains("80%", warning.GetProperty("message").GetString());
        Assert.Equal("limit", limit.GetProperty("level").GetString());
        Assert.Contains("Upgrade to Agent", limit.GetProperty("message").GetString());
        Assert.Equal("agent", limit.GetProperty("upgradeToPlanKey").GetString());
    }

    [Fact]
    public async Task AWorkspaceThatHasUsedItsAiAllowanceIsRefusedBeforeAnyModelIsPaidFor()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var claim = await TestData.CreateCaseAsync(admin, tenant);
        using (var services = host.Services.CreateScope())
        {
            await services.ServiceProvider.GetRequiredService<IWorkspaceAiUsageRecorder>().RecordAsync(
                new WorkspaceAiUsageEntry(TenantScope.Create(tenant, "workspace-main"), "chat:test", "Mock", "mock-chat", null, 400, 200, "c"));
        }

        var summary = await admin.PostAsync($"/api/v1/cases/{claim.CaseId}/summary", null);

        Assert.Equal(HttpStatusCode.PaymentRequired, summary.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(summary);
        Assert.Equal("ai-tokens", body.GetProperty("meter").GetString());
    }

    // ---- buying a plan

    [Fact]
    public async Task PayingWithMobileMoneyStartsThePlanAndSendsAReceipt()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        var payment = await StartAsync(admin);
        Assert.Equal("Pending", payment.GetProperty("status").GetString());
        Assert.Contains("Approve", payment.GetProperty("payerInstruction").GetString());
        Assert.Equal("free", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());

        await SettleAsync(admin, Entry(host, payment));
        var settled = await PaymentAsync(admin, payment);

        Assert.Equal("Succeeded", settled.GetProperty("status").GetString());
        var plan = (await PlanAsync(admin)).GetProperty("current");
        Assert.Equal("agent", plan.GetProperty("key").GetString());
        Assert.Equal("Active", plan.GetProperty("status").GetString());
        Assert.Equal(4, plan.GetProperty("allowances").GetProperty("claimsPerMonth").GetInt32());
        var invoices = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/invoices"));
        var invoice = Assert.Single(invoices.EnumerateArray());
        Assert.Equal("Paid", invoice.GetProperty("status").GetString());
        Assert.Matches(@"^RCT-\d{6}-[2-9A-HJ-NP-Z]{6}$", invoice.GetProperty("receiptNumber").GetString());
        Assert.Equal("MTN Mobile Money", invoice.GetProperty("paidWith").GetString());
        var receipt = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/admin/billing/invoices/{invoice.GetProperty("invoiceId").GetString()}/receipt"));
        Assert.Equal(75000, receipt.GetProperty("invoice").GetProperty("amount").GetInt32());
        var mail = (await OutboxAsync(admin)).Single(message => message.GetProperty("recipient").GetString() == AdminEmail);
        Assert.Contains("Receipt RCT-", mail.GetProperty("subject").GetString());
        Assert.DoesNotContain(Phone, mail.GetProperty("body").GetString());
    }

    [Fact]
    public async Task ThePayersNumberIsNotKeptOnceThePaymentIsSettled()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);
        await SettleAsync(admin, Entry(host, payment));
        await PaymentAsync(admin, payment);

        using var services = host.Services.CreateScope();
        var intent = await services.ServiceProvider.GetRequiredService<IPaymentStore>()
            .GetIntentAsync(TenantScope.Create(tenant, "workspace-main"), Guid.Parse(payment.GetProperty("paymentIntentId").GetString()!));

        Assert.Equal("Succeeded", intent!.Status);
        Assert.Null(intent.PayerContact);
    }

    [Fact]
    public async Task ACardPaymentSendsThePayerToAHostedPageAndNeverTakesCardDetails()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        var payment = await StartAsync(admin, method: "card", currency: "USD", phone: "");

        Assert.Contains("/pay/mock?reference=", payment.GetProperty("redirectUrl").GetString());
        Assert.Equal(20, payment.GetProperty("amount").GetInt32());
        await SettleAsync(admin, Entry(host, payment));
        await PaymentAsync(admin, payment);
        Assert.Equal("agent", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());
    }

    [Fact]
    public async Task ADeclinedPaymentIsShownPlainlyAndChangesNothing()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        var payment = await StartAsync(admin, phone: "256700123499");
        var settled = await PaymentAsync(admin, payment);

        Assert.Equal("Failed", settled.GetProperty("status").GetString());
        Assert.Equal("There was not enough money in the account.", settled.GetProperty("failureReason").GetString());
        Assert.Equal("free", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());
        var invoice = Assert.Single((await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/invoices"))).EnumerateArray());
        Assert.Equal("Open", invoice.GetProperty("status").GetString());
        Assert.Contains((await OutboxAsync(admin)), message => message.GetProperty("subject").GetString() == "Your IIP payment did not go through");
    }

    [Fact]
    public async Task ATryAfterAFailureReusesTheSameInvoice()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var failed = await StartAsync(admin, phone: "256700123499");
        await PaymentAsync(admin, failed);

        var retry = await StartAsync(admin);
        await SettleAsync(admin, Entry(host, retry));
        await PaymentAsync(admin, retry);

        Assert.NotEqual(failed.GetProperty("paymentIntentId").GetString(), retry.GetProperty("paymentIntentId").GetString());
        var invoices = (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/invoices"))).EnumerateArray().ToArray();
        Assert.Single(invoices);
        Assert.Equal("Paid", invoices[0].GetProperty("status").GetString());
        Assert.Equal(2, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/payments"))).GetArrayLength());
    }

    [Fact]
    public async Task PressingTheButtonTwiceAsksThePhoneOnlyOnce()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        var first = await StartAsync(admin);
        var second = await StartAsync(admin);

        Assert.Equal(first.GetProperty("paymentIntentId").GetString(), second.GetProperty("paymentIntentId").GetString());
        Assert.Single(host.Services.GetRequiredService<MockPaymentLedger>().All(), entry => entry.CallbackUrl.Contains(first.GetProperty("paymentIntentId").GetString()!));
    }

    [Theory]
    [InlineData("enterprise", "mtn-momo", "UGX", Phone)]
    [InlineData("free", "mtn-momo", "UGX", Phone)]
    [InlineData("nonsense", "mtn-momo", "UGX", Phone)]
    [InlineData("agent", "mtn-momo", "EUR", Phone)]
    [InlineData("agent", "bitcoin", "UGX", Phone)]
    [InlineData("agent", "mtn-momo", "UGX", "12ab")]
    [InlineData("agent", "mtn-momo", "UGX", "")]
    public async Task ARequestThatCannotBePaidIsRefusedBeforeAnythingIsAskedOfAnOperator(string plan, string method, string currency, string phone)
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        var asked = host.Services.GetRequiredService<MockPaymentLedger>().All().Count;
        var response = await admin.PostAsJsonAsync("/api/v1/admin/billing/checkout", new { planKey = plan, method, currency, payerPhone = phone });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(asked, host.Services.GetRequiredService<MockPaymentLedger>().All().Count);
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/payments"))).EnumerateArray());
    }

    [Fact]
    public async Task WhenPaymentsAreNotSetUpNothingIsOfferedAndNothingPretendsToWork()
    {
        using var host = With(payments: "Disabled");
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        var plan = await PlanAsync(admin);
        var response = await admin.PostAsJsonAsync("/api/v1/admin/billing/checkout", new { planKey = "agent", method = "mtn-momo", currency = "UGX", payerPhone = Phone });

        Assert.Empty(plan.GetProperty("paymentMethods").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not set up", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OnlyAnAdministratorCanBuyAPlan()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        using var officer = host.CreateScopedClient(tenant, role: "ClaimsOfficer", userId: "officer-1");

        var buy = await officer.PostAsJsonAsync("/api/v1/admin/billing/checkout", new { planKey = "agent", method = "mtn-momo", currency = "UGX", payerPhone = Phone });
        var cancel = await officer.PostAsync("/api/v1/admin/billing/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, buy.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/invoices"))).EnumerateArray());
    }

    // ---- callbacks are only a nudge

    [Fact]
    public async Task ACallbackWithTheRightSecretMakesThePlatformAskTheOperatorAndSettles()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);
        var entry = Entry(host, payment);
        host.Services.GetRequiredService<MockPaymentLedger>().Settle(entry.Reference, "Succeeded", null);
        using var anonymous = host.CreateClient();

        var callback = await anonymous.PostAsync(CallbackPath(entry), null);

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Equal("agent", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());
    }

    [Fact]
    public async Task ACallbackCannotMakeAPaymentSucceedWhenTheOperatorSaysItIsStillWaiting()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);
        using var anonymous = host.CreateClient();

        var callback = await anonymous.PostAsJsonAsync(CallbackPath(Entry(host, payment)), new { status = "SUCCESSFUL", amount = 75000 });

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Equal("Pending", (await PaymentAsync(admin, payment)).GetProperty("status").GetString());
        Assert.Equal("free", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());
    }

    [Fact]
    public async Task ACallbackWithTheWrongSecretOrAnUnknownPaymentIsAnswerednotFoundAndDoesNothing()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);
        var entry = Entry(host, payment);
        host.Services.GetRequiredService<MockPaymentLedger>().Settle(entry.Reference, "Succeeded", null);
        using var anonymous = host.CreateClient();
        var id = payment.GetProperty("paymentIntentId").GetString();

        var wrong = await anonymous.PostAsync($"/api/v1/payments/callback/{id}/{new string('0', 48)}", null);
        var unknown = await anonymous.PostAsync($"/api/v1/payments/callback/{Guid.NewGuid()}/{new string('0', 48)}", null);

        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("free", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());
    }

    [Fact]
    public async Task TheCallbackSecretIsNeverStoredOnlyItsFingerprint()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);
        var secret = CallbackPath(Entry(host, payment)).Split('/').Last();

        using var services = host.Services.CreateScope();
        var intent = await services.ServiceProvider.GetRequiredService<IPaymentStore>()
            .GetIntentAsync(TenantScope.Create(tenant, "workspace-main"), Guid.Parse(payment.GetProperty("paymentIntentId").GetString()!));

        Assert.NotNull(intent);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(intent));
        Assert.Equal(PaymentService.Hash(secret), intent.CallbackSecretHash);
    }

    [Fact]
    public async Task ARepeatedCallbackNeverReceiptsTheSamePaymentTwice()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);
        var entry = Entry(host, payment);
        host.Services.GetRequiredService<MockPaymentLedger>().Settle(entry.Reference, "Succeeded", null);
        using var anonymous = host.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsync(CallbackPath(entry), null)).StatusCode);
        }

        var invoice = Assert.Single((await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/invoices"))).EnumerateArray());
        Assert.Equal("Paid", invoice.GetProperty("status").GetString());
        Assert.Single((await OutboxAsync(admin)), message => message.GetProperty("subject").GetString()!.StartsWith("Receipt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APaymentForTheWrongAmountIsNotAcceptedAndNothingIsGranted()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);

        await SettleAsync(admin, Entry(host, payment), amount: 5000);
        var settled = await PaymentAsync(admin, payment);

        Assert.Equal("Failed", settled.GetProperty("status").GetString());
        Assert.Contains("did not match", settled.GetProperty("failureReason").GetString());
        Assert.Equal("free", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());
        using var services = host.Services.CreateScope();
        var audit = await services.ServiceProvider.GetRequiredService<InsuranceIntelligence.Domain.Cases.IAuditRepository>().ListAsync(TenantScope.Create(tenant, "workspace-main"));
        Assert.Contains(audit, item => item.ActionName == "billing.payment.amount_mismatch");
    }

    // ---- isolation

    [Fact]
    public async Task AnotherWorkspaceCannotSeeOrSettleThisWorkspacesPaymentsInvoicesOrReceipts()
    {
        using var host = With();
        var owner = MockedApiFactory.NewTenant();
        var other = MockedApiFactory.NewTenant();
        using var ownerAdmin = await WorkspaceAsync(host, owner);
        using var otherAdmin = await WorkspaceAsync(host, other);
        var payment = await StartAsync(ownerAdmin);
        await SettleAsync(ownerAdmin, Entry(host, payment));
        await PaymentAsync(ownerAdmin, payment);
        var invoice = Assert.Single((await MockedApiFactory.ReadJsonAsync(await ownerAdmin.GetAsync("/api/v1/admin/billing/invoices"))).EnumerateArray());

        var theirPayment = await otherAdmin.GetAsync($"/api/v1/admin/billing/payments/{payment.GetProperty("paymentIntentId").GetString()}");
        var theirReceipt = await otherAdmin.GetAsync($"/api/v1/admin/billing/invoices/{invoice.GetProperty("invoiceId").GetString()}/receipt");

        Assert.Equal(HttpStatusCode.NotFound, theirPayment.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirReceipt.StatusCode);
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await otherAdmin.GetAsync("/api/v1/admin/billing/invoices"))).EnumerateArray());
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await otherAdmin.GetAsync("/api/v1/admin/billing/payments"))).EnumerateArray());
        Assert.Equal("free", (await PlanAsync(otherAdmin)).GetProperty("current").GetProperty("key").GetString());
    }

    // ---- renewal, lapse, cancel, reconciliation

    [Fact]
    public async Task APaidPlanIsInvoicedBeforeItEndsGoesOverdueThenLapsesToFreeAndPayingRestoresIt()
    {
        var time = new ManualTime();
        using var host = With(time);
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var first = await StartAsync(admin);
        await SettleAsync(admin, Entry(host, first));
        await PaymentAsync(admin, first);
        var scope = TenantScope.Create(tenant, "workspace-main");

        async Task<BillingCycleSummary> CycleAsync()
        {
            using var services = host.Services.CreateScope();
            return await services.ServiceProvider.GetRequiredService<SubscriptionLifecycleService>().RunAsync(scope);
        }

        async Task<Subscription> SubscriptionAsync()
        {
            using var services = host.Services.CreateScope();
            return (await services.ServiceProvider.GetRequiredService<ISubscriptionStore>().GetAsync(scope))!;
        }

        // Well inside the paid month: nothing to do.
        Assert.Equal(0, (await CycleAsync()).Examined);

        // Four days before the end: a renewal invoice is issued once, and the administrator is told.
        time.Advance(TimeSpan.FromDays(27));
        var due = await CycleAsync();
        var again = await CycleAsync();
        Assert.Equal(1, due.Invoiced);
        Assert.Equal(0, again.Invoiced);
        var invoices = (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/invoices"))).EnumerateArray().ToArray();
        Assert.Equal(2, invoices.Length);
        Assert.Contains(invoices, invoice => invoice.GetProperty("kind").GetString() == "Renewal" && invoice.GetProperty("status").GetString() == "Open");
        Assert.Contains(await OutboxAsync(admin), message => message.GetProperty("subject").GetString()!.Contains("renews on", StringComparison.Ordinal));

        // The paid period ends unpaid: overdue, still on the plan.
        time.Advance(TimeSpan.FromDays(6));
        Assert.Equal(1, (await CycleAsync()).PastDue);
        Assert.Equal(SubscriptionStatuses.PastDue, (await SubscriptionAsync()).Status);
        Assert.Equal("agent", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());
        Assert.Contains((await PlanAsync(admin)).GetProperty("prompts").EnumerateArray(), prompt => prompt.GetProperty("meter").GetString() == "plan");

        // The grace runs out: held to Free, nothing deleted.
        time.Advance(TimeSpan.FromDays(8));
        Assert.Equal(1, (await CycleAsync()).Lapsed);
        var lapsed = await PlanAsync(admin);
        Assert.Equal("free", lapsed.GetProperty("current").GetProperty("key").GetString());
        Assert.Equal(SubscriptionStatuses.Lapsed, (await SubscriptionAsync()).Status);
        Assert.Equal(2, lapsed.GetProperty("current").GetProperty("allowances").GetProperty("claimsPerMonth").GetInt32());

        // Paying the open invoice restores the paid plan from today.
        var renewal = await StartAsync(admin);
        await SettleAsync(admin, Entry(host, renewal));
        await PaymentAsync(admin, renewal);
        var restored = await SubscriptionAsync();
        Assert.Equal(SubscriptionStatuses.Active, restored.Status);
        Assert.Equal("agent", restored.PlanKey);
        Assert.True(restored.PeriodEnd > time.GetUtcNow().AddDays(27));
        Assert.Equal(2, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/invoices"))).GetArrayLength());
    }

    [Fact]
    public async Task PayingARenewalEarlyExtendsFromTheEndOfTheCurrentPeriodNotFromToday()
    {
        var time = new ManualTime();
        using var host = With(time);
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var first = await StartAsync(admin);
        await SettleAsync(admin, Entry(host, first));
        await PaymentAsync(admin, first);
        var scope = TenantScope.Create(tenant, "workspace-main");
        DateTimeOffset? firstEnd;
        using (var services = host.Services.CreateScope())
        {
            firstEnd = (await services.ServiceProvider.GetRequiredService<ISubscriptionStore>().GetAsync(scope))!.PeriodEnd;
        }

        time.Advance(TimeSpan.FromDays(10));
        var renewal = await StartAsync(admin);
        await SettleAsync(admin, Entry(host, renewal));
        await PaymentAsync(admin, renewal);

        using var after = host.Services.CreateScope();
        var subscription = (await after.ServiceProvider.GetRequiredService<ISubscriptionStore>().GetAsync(scope))!;
        Assert.Equal(firstEnd!.Value.AddMonths(1), subscription.PeriodEnd);
    }

    [Fact]
    public async Task CancellingKeepsThePlanUntilThePaidPeriodEndsThenMovesToFreeAndResumingUndoesIt()
    {
        var time = new ManualTime();
        using var host = With(time);
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var payment = await StartAsync(admin);
        await SettleAsync(admin, Entry(host, payment));
        await PaymentAsync(admin, payment);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/v1/admin/billing/cancel", null)).StatusCode);
        var cancelling = (await PlanAsync(admin)).GetProperty("current");
        Assert.True(cancelling.GetProperty("cancelAtPeriodEnd").GetBoolean());
        Assert.Equal("agent", cancelling.GetProperty("key").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/v1/admin/billing/resume", null)).StatusCode);
        Assert.False((await PlanAsync(admin)).GetProperty("current").GetProperty("cancelAtPeriodEnd").GetBoolean());

        await admin.PostAsync("/api/v1/admin/billing/cancel", null);
        time.Advance(TimeSpan.FromDays(32));
        using (var services = host.Services.CreateScope())
        {
            var summary = await services.ServiceProvider.GetRequiredService<SubscriptionLifecycleService>().RunAsync(TenantScope.Create(tenant, "workspace-main"));
            Assert.Equal(1, summary.Cancelled);
        }

        var after = (await PlanAsync(admin)).GetProperty("current");
        Assert.Equal("free", after.GetProperty("key").GetString());
        Assert.Equal("Active", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ThereIsNothingToCancelOnTheFreePlan()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/billing/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task ReconciliationSettlesAPaymentWhoseCallbackNeverArrivedAndClosesOneNobodyAnswered()
    {
        var time = new ManualTime();
        using var host = With(time);
        var tenant = MockedApiFactory.NewTenant();
        using var admin = await WorkspaceAsync(host, tenant);
        var paid = await StartAsync(admin);
        host.Services.GetRequiredService<MockPaymentLedger>().Settle(Entry(host, paid).Reference, "Succeeded", null);
        var abandoned = await StartAsync(admin, method: "airtel-money", phone: "256750000001");

        async Task<ReconciliationSummary> ReconcileAsync()
        {
            using var services = host.Services.CreateScope();
            return await services.ServiceProvider.GetRequiredService<PaymentReconciliationService>().RunAsync(TenantScope.Create(tenant, "workspace-main"));
        }

        // Too soon: a payment still being approved is left alone.
        Assert.Equal(0, (await ReconcileAsync()).Checked);

        time.Advance(TimeSpan.FromMinutes(5));
        var first = await ReconcileAsync();
        Assert.Equal(1, first.Succeeded);
        Assert.Equal(1, first.StillPending);
        Assert.Equal("agent", (await PlanAsync(admin)).GetProperty("current").GetProperty("key").GetString());

        time.Advance(TimeSpan.FromHours(25));
        var second = await ReconcileAsync();
        Assert.Equal(1, second.Expired);
        var payments = (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing/payments"))).EnumerateArray().ToArray();
        Assert.Contains(payments, payment => payment.GetProperty("paymentIntentId").GetString() == abandoned.GetProperty("paymentIntentId").GetString() && payment.GetProperty("status").GetString() == "Expired");
    }

    // ---- a new company starts on Free

    [Fact]
    public async Task ACompanyThatSignsUpStartsOnTheFreePlanAndItsReceiptsGoToTheAdministrator()
    {
        using var host = With();
        var email = $"signup-{Guid.NewGuid():N}@example.test";
        var company = $"Plan {new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(6).ToArray()).PadRight(6, 'q')} Cover";
        using var anonymous = host.CreateClient();
        var signUp = await anonymous.PostAsJsonAsync("/api/v1/signup", new { companyName = company, adminName = "Pat Plan", email, password = "a-long-enough-phrase-7", acceptTerms = true, website = "" });
        Assert.Equal(HttpStatusCode.Accepted, signUp.StatusCode);
        using var platform = host.CreateScopedClient("iip-platform", "identity");
        string? token = null;
        for (var attempt = 0; attempt < 10 && token is null; attempt++)
        {
            var mail = (await OutboxAsync(platform)).LastOrDefault(message => message.GetProperty("recipient").GetString() == email);
            token = mail.ValueKind == JsonValueKind.Undefined ? null : Regex.Match(mail.GetProperty("body").GetString()!, "token=([A-Za-z0-9_%-]+)") is { Success: true } found ? Uri.UnescapeDataString(found.Groups[1].Value) : null;
            if (token is null) await Task.Delay(100);
        }

        Assert.True((await anonymous.PostAsJsonAsync("/api/v1/signup/verify", new { token })).IsSuccessStatusCode);
        var signedIn = await MockedApiFactory.ReadJsonAsync(await anonymous.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = "a-long-enough-phrase-7" }));
        var user = signedIn.GetProperty("user");

        using var services = host.Services.CreateScope();
        var subscription = await services.ServiceProvider.GetRequiredService<ISubscriptionStore>()
            .GetAsync(TenantScope.Create(user.GetProperty("tenantId").GetString()!, user.GetProperty("workspaceId").GetString()!));
        Assert.NotNull(subscription);
        Assert.Equal("free", subscription.PlanKey);
        Assert.Equal(email, subscription.BillingEmail);
    }
}
