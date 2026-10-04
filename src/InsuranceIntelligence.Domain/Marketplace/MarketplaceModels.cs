namespace InsuranceIntelligence.Domain.Marketplace;

/// <summary>What the Microsoft commercial marketplace says about one SaaS subscription.</summary>
public sealed record MarketplaceSubscriptionInfo(
    string SubscriptionId,
    string OfferId,
    string PlanId,
    int Quantity,
    string Status,
    string? PurchaserEmail,
    string? SubscriptionName)
{
    public const string PendingFulfillmentStart = "PendingFulfillmentStart";
    public const string Subscribed = "Subscribed";
    public const string Suspended = "Suspended";
    public const string Unsubscribed = "Unsubscribed";
}

/// <summary>
/// Microsoft's SaaS Fulfillment API (v2), called only from the backend, never from a browser. The
/// purchase token a buyer arrives with is worthless to anyone else, but only this side can turn it
/// into a subscription, and only this side can tell Microsoft the customer has been set up.
/// </summary>
public interface IMarketplaceFulfillmentClient
{
    bool IsConfigured { get; }

    /// <summary>Exchanges the token on the landing-page URL for the subscription it stands for.</summary>
    Task<MarketplaceSubscriptionInfo?> ResolveAsync(string purchaseToken, CancellationToken cancellationToken = default);

    /// <summary>The subscription as Microsoft holds it now, the authority for status and plan.</summary>
    Task<MarketplaceSubscriptionInfo?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Tells Microsoft the customer is set up so billing can start.</summary>
    Task<bool> ActivateAsync(string subscriptionId, string planId, int quantity, CancellationToken cancellationToken = default);
}
