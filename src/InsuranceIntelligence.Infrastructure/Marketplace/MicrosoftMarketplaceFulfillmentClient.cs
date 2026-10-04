using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using InsuranceIntelligence.Domain.Marketplace;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Marketplace;

/// <summary>
/// Microsoft's SaaS Fulfillment API v2 (api-version 2018-08-31). Authenticates as this publisher's
/// own Microsoft Entra application, with a managed identity or the app registration the Partner
/// Center offer lists, never with a secret kept in configuration.
/// </summary>
public sealed class MicrosoftMarketplaceFulfillmentClient(
    HttpClient httpClient,
    TokenCredential credential,
    ILogger<MicrosoftMarketplaceFulfillmentClient> logger) : IMarketplaceFulfillmentClient
{
    public const string BaseUrl = "https://marketplaceapi.microsoft.com/api/saas";
    public const string ApiVersion = "2018-08-31";
    public const string TokenScope = "20e940b3-4c77-4b0b-9a53-9e16a1b010a7/.default";

    public bool IsConfigured => true;

    public async Task<MarketplaceSubscriptionInfo?> ResolveAsync(string purchaseToken, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"{BaseUrl}/subscriptions/resolve?api-version={ApiVersion}", cancellationToken);
        // The token reaches the landing page URL-encoded; Microsoft expects it decoded.
        request.Headers.TryAddWithoutValidation("x-ms-marketplace-token", Uri.UnescapeDataString(purchaseToken));
        request.Content = new StringContent(string.Empty);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Marketplace resolve was refused with HTTP {StatusCode}.", (int)response.StatusCode);
            return null;
        }

        return Read(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken));
    }

    public async Task<MarketplaceSubscriptionInfo?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, $"{BaseUrl}/subscriptions/{Uri.EscapeDataString(subscriptionId)}?api-version={ApiVersion}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Marketplace get-subscription was refused with HTTP {StatusCode}.", (int)response.StatusCode);
            return null;
        }

        return Read(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken));
    }

    public async Task<bool> ActivateAsync(string subscriptionId, string planId, int quantity, CancellationToken cancellationToken = default)
    {
        using var request = await NewRequestAsync(HttpMethod.Post, $"{BaseUrl}/subscriptions/{Uri.EscapeDataString(subscriptionId)}/activate?api-version={ApiVersion}", cancellationToken);
        request.Content = JsonContent.Create(new { planId, quantity });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Marketplace activate was refused with HTTP {StatusCode}.", (int)response.StatusCode);
        }

        return response.IsSuccessStatusCode;
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext([TokenScope]), cancellationToken);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.TryAddWithoutValidation("x-ms-requestid", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("x-ms-correlationid", Guid.NewGuid().ToString());
        return request;
    }

    /// <summary>Reads both shapes Microsoft returns: the resolve reply (with a nested subscription)
    /// and a plain subscription.</summary>
    public static MarketplaceSubscriptionInfo? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var nested = root.TryGetProperty("subscription", out var inner) && inner.ValueKind == JsonValueKind.Object ? inner : root;
        string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        var id = Text(root, "id") ?? Text(nested, "id");
        var planId = Text(root, "planId") ?? Text(nested, "planId");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(planId))
        {
            return null;
        }

        var quantity = (root.TryGetProperty("quantity", out var q1) && q1.TryGetInt32(out var n1)) ? n1
            : (nested.TryGetProperty("quantity", out var q2) && q2.TryGetInt32(out var n2)) ? n2 : 1;
        var purchaser = nested.TryGetProperty("purchaser", out var p) && p.ValueKind == JsonValueKind.Object ? Text(p, "emailId") : null;
        var status = Text(nested, "saasSubscriptionStatus") ?? Text(root, "saasSubscriptionStatus") ?? MarketplaceSubscriptionInfo.PendingFulfillmentStart;
        return new MarketplaceSubscriptionInfo(
            id, Text(root, "offerId") ?? Text(nested, "offerId") ?? string.Empty, planId, quantity, status, purchaser,
            Text(root, "subscriptionName") ?? Text(nested, "name"));
    }
}

/// <summary>Honest default: without a registered publisher application there is nothing to call.</summary>
public sealed class NotConfiguredMarketplaceFulfillmentClient : IMarketplaceFulfillmentClient
{
    public bool IsConfigured => false;
    public Task<MarketplaceSubscriptionInfo?> ResolveAsync(string purchaseToken, CancellationToken cancellationToken = default) => Task.FromResult<MarketplaceSubscriptionInfo?>(null);
    public Task<MarketplaceSubscriptionInfo?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<MarketplaceSubscriptionInfo?>(null);
    public Task<bool> ActivateAsync(string subscriptionId, string planId, int quantity, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
