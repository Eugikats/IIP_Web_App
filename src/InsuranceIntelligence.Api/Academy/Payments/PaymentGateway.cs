using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InsuranceIntelligence.Api.Academy.Payments;

public sealed record GatewayCheckout(
    string Reference, decimal Amount, string Currency, string Method, string CustomerName, string CustomerEmail,
    string Title, string Description, string RedirectUrl);

/// <summary>What the payment provider itself says about a payment. Nothing here is taken from the browser.</summary>
public sealed record GatewayPayment(bool Successful, string Status, string Reference, string? TransactionId, decimal Amount, string Currency);

/// <summary>The payment provider. IIP's rules about orders, amounts and enrolment never touch its details.</summary>
public interface IPaymentGateway
{
    string Name { get; }

    /// <summary>False when no provider is set up, in which case enrolment stays open and nothing is charged.</summary>
    bool Configured { get; }

    Task<string> CreateCheckoutAsync(GatewayCheckout checkout, CancellationToken cancellationToken);

    /// <summary>Asks the provider about a payment. Null means the provider has no such payment.</summary>
    Task<GatewayPayment?> VerifyAsync(string? transactionId, string reference, CancellationToken cancellationToken);

    /// <summary>Whether a webhook call really came from the provider. With no secret set up this is always false.</summary>
    bool WebhookAuthentic(string? signatureHeader);
}

public sealed class NotConfiguredPaymentGateway : IPaymentGateway
{
    public string Name => "none";

    public bool Configured => false;

    public Task<string> CreateCheckoutAsync(GatewayCheckout checkout, CancellationToken cancellationToken) =>
        throw new AcademyRuleException(409, "Payments are not set up yet.", "payments_not_configured");

    public Task<GatewayPayment?> VerifyAsync(string? transactionId, string reference, CancellationToken cancellationToken) => Task.FromResult<GatewayPayment?>(null);

    public bool WebhookAuthentic(string? signatureHeader) => false;
}

/// <summary>
/// Flutterwave's hosted payment page. The learner pays on Flutterwave's own page, so no card or phone details touch IIP, and
/// every payment is confirmed by asking Flutterwave with the secret key before anyone is enrolled.
/// Settings: Flutterwave:SecretKey, Flutterwave:SecretHash (the webhook secret), Flutterwave:BaseUrl (default the live API).
/// </summary>
public sealed class FlutterwaveGateway(HttpClient http, IConfiguration configuration) : IPaymentGateway
{
    private string? SecretKey => configuration["Flutterwave:SecretKey"];

    private string? SecretHash => configuration["Flutterwave:SecretHash"];

    private string BaseUrl => (configuration["Flutterwave:BaseUrl"] ?? "https://api.flutterwave.com").TrimEnd('/');

    public string Name => "flutterwave";

    public bool Configured => !string.IsNullOrWhiteSpace(SecretKey);

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
        return request;
    }

    public async Task<string> CreateCheckoutAsync(GatewayCheckout checkout, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "/v3/payments");
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            tx_ref = checkout.Reference,
            amount = checkout.Amount,
            currency = checkout.Currency,
            redirect_url = checkout.RedirectUrl,
            payment_options = checkout.Method == "mobile_money" ? "mobilemoneyuganda" : "card",
            customer = new { email = checkout.CustomerEmail, name = checkout.CustomerName },
            customizations = new { title = checkout.Title, description = checkout.Description },
        }), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new AcademyRuleException(502, "The payment page could not be opened. Try again in a moment.", "gateway_error");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (TrySuccessData(document.RootElement, out var data))
            {
                var link = StringProperty(data, "link");
                if (!string.IsNullOrWhiteSpace(link) && Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    return link;
                }
            }
        }
        catch (JsonException)
        {
            // Provider payloads are untrusted. Never return their contents in an error.
        }

        throw new AcademyRuleException(502, "The payment page could not be opened. Try again in a moment.", "gateway_error");
    }

    public async Task<GatewayPayment?> VerifyAsync(string? transactionId, string reference, CancellationToken cancellationToken)
    {
        var path = !string.IsNullOrWhiteSpace(transactionId) && transactionId.All(char.IsDigit)
            ? $"/v3/transactions/{transactionId}/verify"
            : $"/v3/transactions/verify_by_reference?tx_ref={Uri.EscapeDataString(reference)}";
        using var request = Request(HttpMethod.Get, path);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!TrySuccessData(document.RootElement, out var data))
            {
                return null;
            }

            var status = StringProperty(data, "status");
            var tx = StringProperty(data, "tx_ref");
            var currency = StringProperty(data, "currency");
            if (status is null || string.IsNullOrWhiteSpace(tx) || string.IsNullOrWhiteSpace(currency)
                || !data.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.Number || !idValue.TryGetInt64(out var id) || id <= 0
                || !data.TryGetProperty("amount", out var amountValue) || amountValue.ValueKind != JsonValueKind.Number || !amountValue.TryGetDecimal(out var amount) || amount <= 0)
            {
                return null;
            }

            return new GatewayPayment(
                string.Equals(status, "successful", StringComparison.OrdinalIgnoreCase),
                status.ToLowerInvariant(), tx, id.ToString(System.Globalization.CultureInfo.InvariantCulture), amount, currency.ToUpperInvariant());
        }
        catch (JsonException)
        {
            // No usable verification means no enrolment; a later reconcile can try again.
            return null;
        }
    }

    private static string? StringProperty(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private static bool TrySuccessData(JsonElement root, out JsonElement data)
    {
        data = default;
        return string.Equals(StringProperty(root, "status"), "success", StringComparison.OrdinalIgnoreCase)
            && root.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object;
    }

    // This adapter uses v3 Standard: verif-hash is the shared secret itself.
    // v4's flutterwave-signature is a base64 HMAC of the raw body, a different protocol.
    public bool WebhookAuthentic(string? signatureHeader)
    {
        var secret = SecretHash;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(secret);
        var given = Encoding.UTF8.GetBytes(signatureHeader);
        return expected.Length == given.Length && CryptographicOperations.FixedTimeEquals(expected, given);
    }
}

/// <summary>
/// A stand-in for local development and browser tests only (Academy:Payments:Provider=Fake). Its "payment page" is the
/// callback page itself. It is never chosen in a deployed environment.
/// </summary>
public sealed class FakePaymentGateway(IConfiguration configuration) : IPaymentGateway
{
    private static readonly ConcurrentDictionary<string, GatewayCheckout> Created = new();

    public string Name => "fake";

    public bool Configured => true;

    public Task<string> CreateCheckoutAsync(GatewayCheckout checkout, CancellationToken cancellationToken)
    {
        Created[checkout.Reference] = checkout;
        var portal = (configuration["Academy:PortalBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        return Task.FromResult($"{portal}/academy/payment/callback?status=successful&tx_ref={Uri.EscapeDataString(checkout.Reference)}&transaction_id=900{Math.Abs(checkout.Reference.GetHashCode())}");
    }

    public Task<GatewayPayment?> VerifyAsync(string? transactionId, string reference, CancellationToken cancellationToken) =>
        Task.FromResult(Created.TryGetValue(reference, out var checkout)
            ? new GatewayPayment(true, "successful", reference, transactionId ?? "900", checkout.Amount, checkout.Currency)
            : null);

    public bool WebhookAuthentic(string? signatureHeader) => signatureHeader == "fake-hash";
}
