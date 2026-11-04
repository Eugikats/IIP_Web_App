using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InsuranceIntelligence.Domain.Billing;
using Microsoft.Extensions.Configuration;

namespace InsuranceIntelligence.Infrastructure.Payments;

// Live payment adapters for the three methods an East African insurer's customers use. None of them has been run against a real
// account yet (no credentials exist for this build): what is tested is everything around them, and each adapter is small and follows
// its operator's published collection API. A provider with any setting missing reports itself as not configured and is not offered.

public sealed record MtnMomoOptions(string? BaseUrl, string? SubscriptionKey, string? ApiUser, string? ApiKey, string TargetEnvironment)
{
    public bool IsComplete => !string.IsNullOrWhiteSpace(SubscriptionKey) && !string.IsNullOrWhiteSpace(ApiUser) && !string.IsNullOrWhiteSpace(ApiKey);

    public static MtnMomoOptions From(IConfiguration configuration) => new(
        configuration["MtnMomo:BaseUrl"] ?? "https://sandbox.momodeveloper.mtn.com",
        configuration["MtnMomo:SubscriptionKey"],
        configuration["MtnMomo:ApiUser"],
        configuration["MtnMomo:ApiKey"],
        configuration["MtnMomo:TargetEnvironment"] ?? "sandbox");
}

public sealed record AirtelMoneyOptions(string? BaseUrl, string? ClientId, string? ClientSecret, string Country)
{
    public bool IsComplete => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public static AirtelMoneyOptions From(IConfiguration configuration) => new(
        configuration["AirtelMoney:BaseUrl"] ?? "https://openapiuat.airtel.africa",
        configuration["AirtelMoney:ClientId"],
        configuration["AirtelMoney:ClientSecret"],
        configuration["AirtelMoney:Country"] ?? "UG");
}

public sealed record CardGatewayOptions(string? BaseUrl, string? SecretKey)
{
    public bool IsComplete => !string.IsNullOrWhiteSpace(SecretKey);

    public static CardGatewayOptions From(IConfiguration configuration) => new(
        configuration["Flutterwave:BaseUrl"] ?? "https://api.flutterwave.com",
        configuration["Flutterwave:SecretKey"]);
}

internal static class PaymentHttp
{
    public static async Task<JsonNode?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static decimal? Decimal(JsonNode? node) =>
        node is null ? null
        : node is JsonValue value && value.TryGetValue<decimal>(out var number) ? number
        : decimal.TryParse(node.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}

/// <summary>MTN Mobile Money, Collection API: a "request to pay" that the customer approves on their phone.</summary>
public sealed class MtnMomoPaymentProvider(MtnMomoOptions options, HttpClient httpClient) : IPaymentProvider
{
    public IReadOnlyCollection<string> Methods { get; } = [PaymentMethods.MtnMomo];

    public bool IsConfigured => options.IsComplete;

    public async Task<ProviderInitiation> InitiateAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var token = await TokenAsync(cancellationToken);
        var reference = Guid.NewGuid().ToString();
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}/collection/v1_0/requesttopay");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.Add("X-Reference-Id", reference);
        message.Headers.Add("X-Target-Environment", options.TargetEnvironment);
        message.Headers.Add("Ocp-Apim-Subscription-Key", options.SubscriptionKey);
        if (!string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            message.Headers.Add("X-Callback-Url", request.CallbackUrl);
        }

        message.Content = new StringContent(
            new JsonObject
            {
                ["amount"] = request.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                ["currency"] = request.Currency,
                ["externalId"] = request.PaymentIntentId.ToString("N"),
                ["payer"] = new JsonObject { ["partyIdType"] = "MSISDN", ["partyId"] = request.PayerContact },
                ["payerMessage"] = request.Description,
                ["payeeNote"] = request.Description,
            }.ToJsonString(),
            Encoding.UTF8,
            "application/json");
        using var response = await Send(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentProviderException("MTN Mobile Money did not accept the request. Check the number and try again, or choose another way to pay.");
        }

        return new ProviderInitiation(reference, null, "Approve the payment request on your MTN phone. It asks for your PIN.");
    }

    public async Task<ProviderPaymentStatus> GetStatusAsync(string method, string providerReference, CancellationToken cancellationToken = default)
    {
        var token = await TokenAsync(cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl!.TrimEnd('/')}/collection/v1_0/requesttopay/{Uri.EscapeDataString(providerReference)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.Add("X-Target-Environment", options.TargetEnvironment);
        message.Headers.Add("Ocp-Apim-Subscription-Key", options.SubscriptionKey);
        using var response = await Send(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentProviderException("MTN Mobile Money could not be asked about the payment.");
        }

        var body = await PaymentHttp.ReadAsync(response, cancellationToken);
        var status = body?["status"]?.GetValue<string>()?.ToUpperInvariant();
        return status switch
        {
            "SUCCESSFUL" => new ProviderPaymentStatus(PaymentStatuses.Succeeded, PaymentHttp.Decimal(body?["amount"]), body?["currency"]?.GetValue<string>(), null),
            "FAILED" => new ProviderPaymentStatus(PaymentStatuses.Failed, null, null, body?["reason"]?.ToString()),
            _ => new ProviderPaymentStatus(PaymentStatuses.Pending, null, null, null),
        };
    }

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}/collection/token/");
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ApiUser}:{options.ApiKey}")));
        message.Headers.Add("Ocp-Apim-Subscription-Key", options.SubscriptionKey);
        using var response = await Send(message, cancellationToken);
        var body = response.IsSuccessStatusCode ? await PaymentHttp.ReadAsync(response, cancellationToken) : null;
        return body?["access_token"]?.GetValue<string>() ?? throw new PaymentProviderException("MTN Mobile Money is not accepting this service's sign-in. Tell your IIP administrator.");
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new PaymentProviderException("MTN Mobile Money could not be reached. Try again in a moment.");
        }
    }
}

/// <summary>Airtel Money, Collection API v2: a push request that the customer approves on their phone.</summary>
public sealed class AirtelMoneyPaymentProvider(AirtelMoneyOptions options, HttpClient httpClient) : IPaymentProvider
{
    public IReadOnlyCollection<string> Methods { get; } = [PaymentMethods.AirtelMoney];

    public bool IsConfigured => options.IsComplete;

    public async Task<ProviderInitiation> InitiateAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var token = await TokenAsync(cancellationToken);
        var transactionId = request.PaymentIntentId.ToString("N");
        // Airtel wants the number without the country code.
        var msisdn = request.PayerContact ?? string.Empty;
        var dialling = options.Country == "UG" ? "256" : string.Empty;
        if (dialling.Length > 0 && msisdn.StartsWith(dialling, StringComparison.Ordinal))
        {
            msisdn = msisdn[dialling.Length..];
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}/merchant/v2/payments/");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.Add("X-Country", options.Country);
        message.Headers.Add("X-Currency", request.Currency);
        message.Content = new StringContent(
            new JsonObject
            {
                ["reference"] = request.Description,
                ["subscriber"] = new JsonObject { ["country"] = options.Country, ["currency"] = request.Currency, ["msisdn"] = msisdn },
                ["transaction"] = new JsonObject { ["amount"] = request.Amount, ["country"] = options.Country, ["currency"] = request.Currency, ["id"] = transactionId },
            }.ToJsonString(),
            Encoding.UTF8,
            "application/json");
        using var response = await Send(message, cancellationToken);
        var body = await PaymentHttp.ReadAsync(response, cancellationToken);
        if (!response.IsSuccessStatusCode || body?["status"]?["success"]?.GetValue<bool>() != true)
        {
            throw new PaymentProviderException("Airtel Money did not accept the request. Check the number and try again, or choose another way to pay.");
        }

        return new ProviderInitiation(transactionId, null, "Approve the payment request on your Airtel phone. It asks for your PIN.");
    }

    public async Task<ProviderPaymentStatus> GetStatusAsync(string method, string providerReference, CancellationToken cancellationToken = default)
    {
        var token = await TokenAsync(cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl!.TrimEnd('/')}/standard/v1/payments/{Uri.EscapeDataString(providerReference)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        message.Headers.Add("X-Country", options.Country);
        message.Headers.Add("X-Currency", "UGX");
        using var response = await Send(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentProviderException("Airtel Money could not be asked about the payment.");
        }

        var body = await PaymentHttp.ReadAsync(response, cancellationToken);
        var transaction = body?["data"]?["transaction"];
        // TS success, TF failed, TE expired, TIP in progress, TA ambiguous (treated as still pending until it resolves).
        return transaction?["status"]?.GetValue<string>()?.ToUpperInvariant() switch
        {
            "TS" => new ProviderPaymentStatus(PaymentStatuses.Succeeded, null, null, null),
            "TF" => new ProviderPaymentStatus(PaymentStatuses.Failed, null, null, transaction?["message"]?.GetValue<string>()),
            "TE" => new ProviderPaymentStatus(PaymentStatuses.Expired, null, null, "The request timed out before it was approved."),
            _ => new ProviderPaymentStatus(PaymentStatuses.Pending, null, null, null),
        };
    }

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}/auth/oauth2/token")
        {
            Content = new StringContent(
                new JsonObject { ["client_id"] = options.ClientId, ["client_secret"] = options.ClientSecret, ["grant_type"] = "client_credentials" }.ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
        using var response = await Send(message, cancellationToken);
        var body = response.IsSuccessStatusCode ? await PaymentHttp.ReadAsync(response, cancellationToken) : null;
        return body?["access_token"]?.GetValue<string>() ?? throw new PaymentProviderException("Airtel Money is not accepting this service's sign-in. Tell your IIP administrator.");
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new PaymentProviderException("Airtel Money could not be reached. Try again in a moment.");
        }
    }
}

/// <summary>A hosted card page (Flutterwave Standard): the payer is sent to the gateway to enter card details, which never touch IIP.</summary>
public sealed class FlutterwaveCardPaymentProvider(CardGatewayOptions options, HttpClient httpClient) : IPaymentProvider
{
    public IReadOnlyCollection<string> Methods { get; } = [PaymentMethods.Card];

    public bool IsConfigured => options.IsComplete;

    public async Task<ProviderInitiation> InitiateAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var reference = request.PaymentIntentId.ToString("N");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}/v3/payments");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);
        message.Content = new StringContent(
            new JsonObject
            {
                ["tx_ref"] = reference,
                ["amount"] = request.Amount,
                ["currency"] = request.Currency,
                ["redirect_url"] = string.IsNullOrWhiteSpace(request.ReturnUrl) ? null : request.ReturnUrl,
                ["payment_options"] = "card",
                ["customer"] = new JsonObject { ["email"] = request.PayerContact },
                ["customizations"] = new JsonObject { ["title"] = "IIP", ["description"] = request.Description },
            }.ToJsonString(),
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new PaymentProviderException("The card payment page could not be reached. Try again in a moment.");
        }

        using (response)
        {
            var body = await PaymentHttp.ReadAsync(response, cancellationToken);
            var link = body?["data"]?["link"]?.GetValue<string>();
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(link))
            {
                throw new PaymentProviderException("The card payment page could not be opened. Try again, or choose another way to pay.");
            }

            return new ProviderInitiation(reference, link, "You will be taken to a secure page to enter your card details.");
        }
    }

    public async Task<ProviderPaymentStatus> GetStatusAsync(string method, string providerReference, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl!.TrimEnd('/')}/v3/transactions/verify_by_reference?tx_ref={Uri.EscapeDataString(providerReference)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new PaymentProviderException("The card gateway could not be asked about the payment.");
        }

        using (response)
        {
            var body = await PaymentHttp.ReadAsync(response, cancellationToken);
            // Until the payer has tried to pay, the gateway has no such transaction; that is still waiting, not failed.
            if (!response.IsSuccessStatusCode || body?["status"]?.GetValue<string>() != "success")
            {
                return new ProviderPaymentStatus(PaymentStatuses.Pending, null, null, null);
            }

            var data = body["data"];
            return data?["status"]?.GetValue<string>()?.ToLowerInvariant() switch
            {
                "successful" => new ProviderPaymentStatus(PaymentStatuses.Succeeded, PaymentHttp.Decimal(data?["amount"]), data?["currency"]?.GetValue<string>(), null),
                "failed" or "cancelled" => new ProviderPaymentStatus(PaymentStatuses.Failed, null, null, data?["processor_response"]?.GetValue<string>()),
                _ => new ProviderPaymentStatus(PaymentStatuses.Pending, null, null, null),
            };
        }
    }
}

public sealed class PaymentProviderSelector(IEnumerable<IPaymentProvider> providers) : IPaymentProviderSelector
{
    private readonly IPaymentProvider[] configured = providers.Where(provider => provider.IsConfigured).ToArray();

    public IPaymentProvider? For(string method) => configured.FirstOrDefault(provider => provider.Methods.Contains(method));

    public IReadOnlyList<string> AvailableMethods =>
        PaymentMethods.All.Where(method => configured.Any(provider => provider.Methods.Contains(method))).ToArray();
}
