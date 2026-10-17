using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Academy;
using InsuranceIntelligence.Api.Academy.Payments;
using Microsoft.Extensions.Configuration;

namespace InsuranceIntelligence.Api.Tests;

public sealed class FlutterwaveGatewayTests
{
    private const string Verified = """
        {"status":"success","data":{"id":777,"status":"successful","tx_ref":"IIPA-test","amount":85,"currency":"USD"}}
        """;

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private static FlutterwaveGateway Gateway(HttpClient http, string? secretHash = "test-only-webhook-value") => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Flutterwave:SecretKey"] = "test-only-placeholder",
            ["Flutterwave:SecretHash"] = secretHash,
        }).Build());

    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static GatewayCheckout Checkout(string method = "card", string currency = "USD", decimal amount = 85) =>
        new("IIPA-test", amount, currency, method, "Test learner", "learner@example.test", "IIP Academy", "Course", "https://portal.example.test/academy/payment/callback");

    [Theory]
    [InlineData("card", "USD", 85, "card")]
    [InlineData("card", "UGX", 350000, "card")]
    [InlineData("mobile_money", "UGX", 350000, "mobilemoneyuganda")]
    public async Task Checkout_UsesTheV3StandardRequest(string method, string currency, decimal amount, string option)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.flutterwave.com/v3/payments", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-only-placeholder", request.Headers.Authorization.Parameter);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            using var document = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.Equal("IIPA-test", root.GetProperty("tx_ref").GetString());
            Assert.Equal(amount, root.GetProperty("amount").GetDecimal());
            Assert.Equal(currency, root.GetProperty("currency").GetString());
            Assert.Equal(option, root.GetProperty("payment_options").GetString());
            Assert.Equal(Checkout().RedirectUrl, root.GetProperty("redirect_url").GetString());
            Assert.Equal("learner@example.test", root.GetProperty("customer").GetProperty("email").GetString());
            Assert.Equal("Test learner", root.GetProperty("customer").GetProperty("name").GetString());
            Assert.Equal("IIP Academy", root.GetProperty("customizations").GetProperty("title").GetString());
            Assert.Equal("Course", root.GetProperty("customizations").GetProperty("description").GetString());
            return Response("""{"status":"success","data":{"link":"https://checkout.flutterwave.com/v3/hosted/pay/test"}}""");
        }));

        Assert.Equal("https://checkout.flutterwave.com/v3/hosted/pay/test", await Gateway(http).CreateCheckoutAsync(Checkout(method, currency, amount), default));
    }

    [Theory]
    [InlineData(200, "not json")]
    [InlineData(200, "null")]
    [InlineData(200, "[]")]
    [InlineData(200, "{\"status\":\"success\",\"data\":null}")]
    [InlineData(200, "{\"status\":\"error\",\"data\":{\"link\":\"https://checkout.flutterwave.com/test\"}}")]
    [InlineData(200, "{\"data\":{\"link\":\"https://checkout.flutterwave.com/test\"}}")]
    [InlineData(200, "{\"status\":\"success\",\"data\":{\"link\":42}}")]
    [InlineData(200, "{\"status\":\"success\",\"data\":{\"link\":\"http://checkout.flutterwave.com/test\"}}")]
    [InlineData(200, "{\"status\":\"success\",\"data\":{\"link\":\"/relative\"}}")]
    [InlineData(200, "{\"status\":\"success\",\"data\":{\"link\":\"javascript:alert(1)\"}}")]
    [InlineData(500, "private provider error")]
    [InlineData(400, "private provider error")]
    [InlineData(201, "{\"status\":\"success\",\"data\":{\"link\":\"https://checkout.flutterwave.com/test\"}}")]
    [InlineData(204, "")]
    public async Task Checkout_RejectsUnusableResponsesWithASafeError(int status, string body)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Response(body, (HttpStatusCode)status))));
        var error = await Assert.ThrowsAsync<AcademyRuleException>(() => Gateway(http).CreateCheckoutAsync(Checkout(), default));
        Assert.Equal(502, error.Status);
        Assert.Equal("gateway_error", error.Code);
        Assert.Equal("The payment page could not be opened. Try again in a moment.", error.Message);
    }

    [Theory]
    [InlineData("777", "/v3/transactions/777/verify")]
    [InlineData(null, "/v3/transactions/verify_by_reference?tx_ref=IIPA-test%2F%3F%26")]
    [InlineData("not-an-id", "/v3/transactions/verify_by_reference?tx_ref=IIPA-test%2F%3F%26")]
    public async Task Verify_UsesIdOrEscapedReference_AndReadsTheV3Response(string? id, string path)
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.flutterwave.com" + path, request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(Response(Verified));
        }));
        var payment = await Gateway(http).VerifyAsync(id, "IIPA-test/?&", default);
        Assert.Equal(new GatewayPayment(true, "successful", "IIPA-test", "777", 85, "USD"), payment);
    }

    [Theory]
    [InlineData("777")]
    [InlineData(null)]
    public async Task Verify_ReportsFailedTransactionsWithoutAuthorizingEnrolment(string? id)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Response(Verified.Replace("successful", "failed")))));
        var payment = await Gateway(http).VerifyAsync(id, "IIPA-test", default);
        Assert.NotNull(payment);
        Assert.False(payment.Successful);
        Assert.Equal("failed", payment.Status);
    }

    [Theory]
    [InlineData(400, "{\"status\":\"error\",\"message\":\"No transaction was found for this id\",\"data\":null}")]
    [InlineData(404, "not found")]
    [InlineData(500, "private provider error")]
    [InlineData(201, "{\"status\":\"success\",\"data\":{\"id\":777,\"status\":\"successful\",\"tx_ref\":\"IIPA-test\",\"amount\":85,\"currency\":\"USD\"}}")]
    [InlineData(204, "")]
    [InlineData(200, "{\"status\":\"success\",\"data\":null}")]
    [InlineData(200, "not json")]
    [InlineData(200, "null")]
    [InlineData(200, "[]")]
    public async Task Verify_UnusableResponsesCannotConfirmAPayment(int status, string body)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Response(body, (HttpStatusCode)status))));
        Assert.Null(await Gateway(http).VerifyAsync("777", "IIPA-test", default));
        Assert.Null(await Gateway(http).VerifyAsync(null, "IIPA-test", default));
    }

    [Theory]
    [InlineData("\"status\":\"success\"", "\"status\":\"error\"")]
    [InlineData("\"status\":\"success\"", "\"status\":42")]
    [InlineData("\"amount\":85", "\"amount\":\"85\"")]
    [InlineData("\"amount\":85", "\"amount\":null")]
    [InlineData("\"amount\":85", "\"amount\":-1")]
    [InlineData("\"currency\":\"USD\"", "\"currency\":null")]
    [InlineData("\"tx_ref\":\"IIPA-test\"", "\"tx_ref\":42")]
    [InlineData("\"id\":777", "\"id\":null")]
    public async Task Verify_InvalidEnvelopeOrFieldsCannotConfirmAPayment(string before, string after)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Response(Verified.Replace(before, after)))));
        Assert.Null(await Gateway(http).VerifyAsync("777", "IIPA-test", default));
        Assert.Null(await Gateway(http).VerifyAsync(null, "IIPA-test", default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Webhook_RejectsEverySignatureWithoutAConfiguredSecret(string? secret)
    {
        using var http = new HttpClient();
        var gateway = Gateway(http, secret);
        Assert.False(gateway.WebhookAuthentic(secret));
        Assert.False(gateway.WebhookAuthentic("anything"));
    }

    [Fact]
    public void Webhook_RequiresTheExactV3Hash_AndCallsTheConstantTimePrimitive()
    {
        using var http = new HttpClient();
        var gateway = Gateway(http);
        Assert.True(gateway.WebhookAuthentic("test-only-webhook-value"));
        foreach (var signature in new[] { null, "", "Test-only-webhook-value", "test-only-webhook-valuE", "test-only-webhook-value ", "short" })
        {
            Assert.False(gateway.WebhookAuthentic(signature));
        }

        // v4 uses a body HMAC. It must not be accepted as the v3 shared hash.
        var hmac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-only-webhook-value"), Encoding.UTF8.GetBytes("{}"));
        Assert.False(gateway.WebhookAuthentic(Convert.ToBase64String(hmac)));

        // A structural regression check avoids unreliable timing benchmarks.
        var method = typeof(FlutterwaveGateway).GetMethod(nameof(FlutterwaveGateway.WebhookAuthentic))!;
        var il = method.GetMethodBody()!.GetILAsByteArray()!;
        var callsFixedTimeEquals = false;
        for (var offset = 0; offset < il.Length - 4; offset++)
        {
            if (il[offset] != 0x28) continue; // call
            try
            {
                var target = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset + 1));
                callsFixedTimeEquals |= target?.DeclaringType == typeof(CryptographicOperations)
                    && target.Name == nameof(CryptographicOperations.FixedTimeEquals);
            }
            catch (ArgumentException)
            {
                // The byte may be part of another instruction's operand.
            }
        }

        Assert.True(callsFixedTimeEquals);
    }
}
