using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Marketplace;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Domain.Usage;

namespace InsuranceIntelligence.Application.Marketplace;

public sealed record ResolvedPurchase(
    string SubscriptionId, string OfferId, string PlanId, string Status, string? PurchaserEmail, string? ExistingOrganisationId);

/// <summary>Maps the plan ids published in Partner Center to this platform's plans. An unknown
/// plan gets the smallest, so a mapping mistake can never hand out unlimited use.</summary>
public sealed record MarketplacePlanMap(IReadOnlyDictionary<string, string> Entries)
{
    public static MarketplacePlanMap Parse(string? text)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
            {
                entries[parts[0]] = parts[1];
            }
        }

        return new MarketplacePlanMap(entries);
    }

    public string PlanFor(string marketplacePlanId) =>
        Entries.TryGetValue(marketplacePlanId, out var mapped) ? PlanCatalog.Find(mapped).PlanId : PlanCatalog.Find(marketplacePlanId).PlanId;
}

/// <summary>
/// Turns a Microsoft Marketplace purchase into an organisation, and keeps the organisation in step
/// with the subscription afterwards. Microsoft is the authority on status and plan: a webhook is
/// only ever a reason to ask Microsoft again, so a forged one can trigger a re-check and nothing else.
/// </summary>
public sealed class MarketplaceFulfillmentService(
    IMarketplaceFulfillmentClient client,
    IOrganisationRepository organisations,
    OrganisationService organisationService,
    IAuditRepository auditRepository,
    MarketplacePlanMap planMap)
{
    private static readonly TenantScope PlatformScope = TenantScope.Create("iip-platform", "organisations");

    public async Task<ResolvedPurchase> ResolveAsync(string purchaseToken, CancellationToken cancellationToken = default)
    {
        var subscription = await RequireConfiguredResolveAsync(purchaseToken, cancellationToken);
        var existing = await organisations.GetByMarketplaceSubscriptionAsync(subscription.SubscriptionId, cancellationToken);
        return new ResolvedPurchase(
            subscription.SubscriptionId, subscription.OfferId, planMap.PlanFor(subscription.PlanId), subscription.Status,
            subscription.PurchaserEmail, existing?.OrganisationId);
    }

    /// <summary>The buyer names their organisation and becomes its administrator. Safe to repeat.</summary>
    public async Task<Organisation> ActivateAsync(
        string userId, string organisationName, string purchaseToken, string correlationId, CancellationToken cancellationToken = default)
    {
        var subscription = await RequireConfiguredResolveAsync(purchaseToken, cancellationToken);
        if (subscription.Status is MarketplaceSubscriptionInfo.Unsubscribed or MarketplaceSubscriptionInfo.Suspended)
        {
            throw OrganisationException.Conflict("This subscription is not active in Microsoft Marketplace.");
        }

        var organisation = await organisations.GetByMarketplaceSubscriptionAsync(subscription.SubscriptionId, cancellationToken);
        if (organisation is null)
        {
            organisation = await organisationService.CreateAsync(userId, organisationName, OrganisationType.Insurer, correlationId, cancellationToken);
            organisation = organisation with
            {
                PlanId = planMap.PlanFor(subscription.PlanId),
                MarketplaceSubscriptionId = subscription.SubscriptionId,
            };
            await organisations.UpdateOrganisationAsync(organisation, cancellationToken);
            await AuditAsync(userId, "marketplace.subscription_bound", organisation, correlationId, cancellationToken);
        }
        else
        {
            // A second person arriving with the same purchase is not the buyer's organisation.
            var mine = await organisationService.ListMineAsync(userId, cancellationToken);
            if (!mine.Any(m => m.OrganisationId == organisation.OrganisationId))
            {
                throw OrganisationException.Forbidden("This subscription has already been set up by someone else.");
            }
        }

        // Billing starts only when Microsoft is told. If that call failed earlier, this repeats it.
        if (subscription.Status == MarketplaceSubscriptionInfo.PendingFulfillmentStart)
        {
            if (!await client.ActivateAsync(subscription.SubscriptionId, subscription.PlanId, Math.Max(1, subscription.Quantity), cancellationToken))
            {
                throw new OrganisationException("upstream", "Microsoft Marketplace did not accept the activation. Try again in a moment.");
            }

            await AuditAsync(userId, "marketplace.subscription_activated", organisation, correlationId, cancellationToken);
        }

        return organisation;
    }

    /// <summary>Brings the organisation in line with what Microsoft says about its subscription now.</summary>
    public async Task<string> SyncAsync(string subscriptionId, string correlationId, CancellationToken cancellationToken = default)
    {
        var organisation = string.IsNullOrWhiteSpace(subscriptionId)
            ? null
            : await organisations.GetByMarketplaceSubscriptionAsync(subscriptionId.Trim(), cancellationToken);
        if (organisation is null)
        {
            return "unknown";
        }

        var subscription = await client.GetSubscriptionAsync(organisation.MarketplaceSubscriptionId!, cancellationToken);
        if (subscription is null)
        {
            return "unavailable";
        }

        var status = subscription.Status switch
        {
            MarketplaceSubscriptionInfo.Subscribed => Organisation.ActiveStatus,
            MarketplaceSubscriptionInfo.Suspended => Organisation.SuspendedStatus,
            MarketplaceSubscriptionInfo.Unsubscribed => Organisation.CancelledStatus,
            _ => organisation.Status,
        };
        var plan = planMap.PlanFor(subscription.PlanId);
        if (status == organisation.Status && plan == organisation.PlanId)
        {
            return "unchanged";
        }

        // Never reopen an organisation that closed itself because Microsoft says it is subscribed.
        if (organisation.Status == Organisation.ClosedStatus)
        {
            return "closed";
        }

        var updated = organisation with { Status = status, PlanId = plan };
        await organisations.UpdateOrganisationAsync(updated, cancellationToken);
        await AuditAsync("system:marketplace", "marketplace.subscription_changed", updated, correlationId, cancellationToken);
        return "updated";
    }

    private async Task<MarketplaceSubscriptionInfo> RequireConfiguredResolveAsync(string purchaseToken, CancellationToken cancellationToken)
    {
        if (!client.IsConfigured)
        {
            throw new OrganisationException("not_configured", "Microsoft Marketplace is not connected on this deployment.");
        }

        if (string.IsNullOrWhiteSpace(purchaseToken))
        {
            throw OrganisationException.Invalid("The purchase link is missing its token. Open the link from Microsoft again.");
        }

        return await client.ResolveAsync(purchaseToken.Trim(), cancellationToken)
            ?? throw OrganisationException.NotFound("Microsoft could not match this purchase link. It may have expired; open it from Microsoft again.");
    }

    private Task AuditAsync(string userId, string action, Organisation organisation, string correlationId, CancellationToken cancellationToken) =>
        auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), PlatformScope, null, null, userId, ActorType.System, action, DateTimeOffset.UtcNow,
                null, organisation.Status, [organisation.OrganisationId], "Marketplace", null, "recorded", null,
                $"Plan {organisation.PlanId}, subscription {organisation.MarketplaceSubscriptionId}.", correlationId),
            cancellationToken);
}
