using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Infrastructure.Payments;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The three live payment adapters, run against a stand-in for each operator's API. This proves the requests are shaped as the operators
/// document them and that every answer is mapped to the platform's own statuses. It does not prove the operators accept them: that needs
/// real accounts and is on the real-credentials checklist.
/// </summary>
public sealed class LivePaymentProviderTests
{
    private sealed class Operator(Func<HttpRequestMessage, string?, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request, body));
            var (status, text) = respond(request, body);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static ProviderPaymentRequest Request(string method, string currency = "UGX", string? contact = "256700123456") =>
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"), method, 75000, currency, "IIP Agent plan, one month (INV-1)", contact, "https://api.example.test/api/v1/payments/callback/x/y", "https://app.example.test/app/admin/billing");

    // ---- MTN

    private static MtnMomoPaymentProvider Mtn(Operator op, bool complete = true) =>
        new(new MtnMomoOptions("https://momo.example.test", complete ? "sub-key" : null, "api-user", "api-key", "mtnuganda"), new HttpClient(op));

    [Fact]
    public async Task MtnAskTheCustomerToApproveWithTheDocumentedHeadersAndBody()
    {
        var op = new Operator((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/token/", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"access_token":"tok","expires_in":3600}""")
            : (HttpStatusCode.Accepted, ""));

        var initiation = await Mtn(op).InitiateAsync(Request(PaymentMethods.MtnMomo));

        var token = op.Calls[0].Request;
        Assert.Equal("Basic", token.Headers.Authorization!.Scheme);
        Assert.Equal("sub-key", token.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        var pay = op.Calls[1];
        Assert.Equal("/collection/v1_0/requesttopay", pay.Request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", pay.Request.Headers.Authorization!.Scheme);
        Assert.Equal("mtnuganda", pay.Request.Headers.GetValues("X-Target-Environment").Single());
        Assert.Equal(initiation.ProviderReference, pay.Request.Headers.GetValues("X-Reference-Id").Single());
        Assert.Equal("https://api.example.test/api/v1/payments/callback/x/y", pay.Request.Headers.GetValues("X-Callback-Url").Single());
        var body = JsonNode.Parse(pay.Body!)!;
        Assert.Equal("75000", body["amount"]!.GetValue<string>());
        Assert.Equal("UGX", body["currency"]!.GetValue<string>());
        Assert.Equal("MSISDN", body["payer"]!["partyIdType"]!.GetValue<string>());
        Assert.Equal("256700123456", body["payer"]!["partyId"]!.GetValue<string>());
        Assert.Null(initiation.RedirectUrl);
        Assert.Contains("PIN", initiation.PayerInstruction);
    }

    [Theory]
    [InlineData("SUCCESSFUL", "Succeeded")]
    [InlineData("FAILED", "Failed")]
    [InlineData("PENDING", "Pending")]
    [InlineData("SOMETHING-NEW", "Pending")]
    public async Task MtnStatusesAreMappedAndTheAmountComesBackForChecking(string operatorStatus, string expected)
    {
        var op = new Operator((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/token/", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"access_token":"tok"}""")
            : (HttpStatusCode.OK, "{\"amount\":\"75000\",\"currency\":\"UGX\",\"status\":\"" + operatorStatus + "\",\"reason\":{\"code\":\"X\",\"message\":\"insufficient balance\"}}"));

        var status = await Mtn(op).GetStatusAsync(PaymentMethods.MtnMomo, "ref-1");

        Assert.Equal(expected, status.Status);
        if (expected == "Succeeded")
        {
            Assert.Equal((75000m, "UGX"), (status.Amount, status.Currency));
        }
    }

    [Fact]
    public async Task MtnRefusalsAndOutagesBecomePlainSentencesNeverRawProviderText()
    {
        var refusing = new Operator((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/token/", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"access_token":"tok"}""")
            : (HttpStatusCode.BadRequest, """{"code":"PAYER_NOT_FOUND","message":"secret internal detail"}"""));
        var rejected = await Assert.ThrowsAsync<PaymentProviderException>(() => Mtn(refusing).InitiateAsync(Request(PaymentMethods.MtnMomo)));
        Assert.DoesNotContain("secret internal detail", rejected.Message);

        var noSignIn = new Operator((_, _) => (HttpStatusCode.Unauthorized, "{}"));
        await Assert.ThrowsAsync<PaymentProviderException>(() => Mtn(noSignIn).InitiateAsync(Request(PaymentMethods.MtnMomo)));
    }

    [Fact]
    public void AProviderMissingItsCredentialsIsNotConfiguredAndIsNotOffered()
    {
        var mtn = Mtn(new Operator((_, _) => (HttpStatusCode.OK, "{}")), complete: false);
        var airtel = new AirtelMoneyPaymentProvider(new AirtelMoneyOptions("https://airtel.example.test", null, null, "UG"), new HttpClient());
        var card = new FlutterwaveCardPaymentProvider(new CardGatewayOptions("https://card.example.test", "sk"), new HttpClient());

        var selector = new PaymentProviderSelector([mtn, airtel, card]);

        Assert.False(mtn.IsConfigured);
        Assert.False(airtel.IsConfigured);
        Assert.Equal([PaymentMethods.Card], selector.AvailableMethods);
        Assert.Null(selector.For(PaymentMethods.MtnMomo));
        Assert.NotNull(selector.For(PaymentMethods.Card));
    }

    // ---- Airtel

    private static AirtelMoneyPaymentProvider Airtel(Operator op) =>
        new(new AirtelMoneyOptions("https://airtel.example.test", "client", "secret", "UG"), new HttpClient(op));

    [Fact]
    public async Task AirtelPushesARequestWithTheNumberLeftWithoutItsCountryCode()
    {
        var op = new Operator((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"access_token":"tok"}""")
            : (HttpStatusCode.OK, """{"data":{"transaction":{"id":"x","status":"TIP"}},"status":{"success":true}}"""));

        var initiation = await Airtel(op).InitiateAsync(Request(PaymentMethods.AirtelMoney));

        var pay = op.Calls[1];
        Assert.Equal("/merchant/v2/payments/", pay.Request.RequestUri!.AbsolutePath);
        Assert.Equal("UG", pay.Request.Headers.GetValues("X-Country").Single());
        Assert.Equal("UGX", pay.Request.Headers.GetValues("X-Currency").Single());
        var body = JsonNode.Parse(pay.Body!)!;
        Assert.Equal("700123456", body["subscriber"]!["msisdn"]!.GetValue<string>());
        Assert.Equal(75000, body["transaction"]!["amount"]!.GetValue<decimal>());
        Assert.Equal("11111111222233334444555555555555", body["transaction"]!["id"]!.GetValue<string>());
        Assert.Equal("11111111222233334444555555555555", initiation.ProviderReference);
    }

    [Theory]
    [InlineData("TS", "Succeeded")]
    [InlineData("TF", "Failed")]
    [InlineData("TE", "Expired")]
    [InlineData("TIP", "Pending")]
    [InlineData("TA", "Pending")]
    public async Task AirtelStatusCodesAreMapped(string code, string expected)
    {
        var op = new Operator((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"access_token":"tok"}""")
            : (HttpStatusCode.OK, "{\"data\":{\"transaction\":{\"id\":\"x\",\"message\":\"Insufficient funds\",\"status\":\"" + code + "\"}}}"));

        Assert.Equal(expected, (await Airtel(op).GetStatusAsync(PaymentMethods.AirtelMoney, "x")).Status);
    }

    [Fact]
    public async Task AirtelRefusingTheRequestIsReportedPlainly()
    {
        var op = new Operator((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"access_token":"tok"}""")
            : (HttpStatusCode.OK, """{"status":{"success":false,"message":"ESB000011"}}"""));

        var thrown = await Assert.ThrowsAsync<PaymentProviderException>(() => Airtel(op).InitiateAsync(Request(PaymentMethods.AirtelMoney)));

        Assert.DoesNotContain("ESB000011", thrown.Message);
    }

    // ---- Card

    private static FlutterwaveCardPaymentProvider Card(Operator op) =>
        new(new CardGatewayOptions("https://card.example.test", "sk-live"), new HttpClient(op));

    [Fact]
    public async Task TheCardGatewayIsAskedForAHostedPageAndNoCardDetailIsEverSent()
    {
        var op = new Operator((_, _) => (HttpStatusCode.OK, """{"status":"success","data":{"link":"https://checkout.example.test/pay/abc"}}"""));

        var initiation = await Card(op).InitiateAsync(Request(PaymentMethods.Card, "USD", "admin@example.test"));

        var call = op.Calls.Single();
        Assert.Equal("/v3/payments", call.Request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", call.Request.Headers.Authorization!.Scheme);
        var body = JsonNode.Parse(call.Body!)!;
        Assert.Equal("11111111222233334444555555555555", body["tx_ref"]!.GetValue<string>());
        Assert.Equal("card", body["payment_options"]!.GetValue<string>());
        Assert.Equal("admin@example.test", body["customer"]!["email"]!.GetValue<string>());
        Assert.Equal("https://app.example.test/app/admin/billing", body["redirect_url"]!.GetValue<string>());
        foreach (var forbidden in new[] { "card_number", "cvv", "expiry", "pan" })
        {
            Assert.DoesNotContain(forbidden, call.Body!, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal("https://checkout.example.test/pay/abc", initiation.RedirectUrl);
    }

    [Theory]
    [InlineData("""{"status":"success","data":{"status":"successful","amount":20,"currency":"USD"}}""", "Succeeded")]
    [InlineData("""{"status":"success","data":{"status":"failed","processor_response":"Declined"}}""", "Failed")]
    [InlineData("""{"status":"success","data":{"status":"pending"}}""", "Pending")]
    [InlineData("""{"status":"error","message":"No transaction was found for this id"}""", "Pending")]
    public async Task CardVerificationIsMappedAndAMissingTransactionIsStillWaitingNotFailed(string answer, string expected)
    {
        var op = new Operator((_, _) => (HttpStatusCode.OK, answer));

        var status = await Card(op).GetStatusAsync(PaymentMethods.Card, "11111111222233334444555555555555");

        Assert.Equal(expected, status.Status);
        Assert.Contains("verify_by_reference", op.Calls.Single().Request.RequestUri!.AbsolutePath);
        if (expected == "Succeeded")
        {
            Assert.Equal((20m, "USD"), (status.Amount, status.Currency));
        }
    }
}
