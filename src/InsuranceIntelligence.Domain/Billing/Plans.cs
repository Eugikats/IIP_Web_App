using System.Text.Json;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Billing;

/// <summary>What a plan lets a workspace do in a calendar month. 0 means the plan sets no limit on that dimension.</summary>
public sealed record PlanAllowances(int ClaimsPerMonth, int DocumentsPerMonth, long AiTokensPerMonth, int StorageMb, int Seats)
{
    public static readonly PlanAllowances Unlimited = new(0, 0, 0, 0, 0);
}

/// <summary>A price for one calendar month in one currency, in whole units of that currency (UGX has no minor unit).</summary>
public sealed record PlanPrice(string Currency, decimal MonthlyAmount);

public sealed record Plan(string Key, string Name, string Summary, int Rank, bool SelfServe, IReadOnlyList<PlanPrice> Prices, PlanAllowances Allowances)
{
    public decimal? PriceIn(string currency) =>
        Prices.FirstOrDefault(price => string.Equals(price.Currency, currency, StringComparison.OrdinalIgnoreCase))?.MonthlyAmount;

    public bool IsFree => SelfServe && Prices.All(price => price.MonthlyAmount == 0);
}

/// <summary>
/// The plans a workspace can be on. The defaults below are list prices and allowances to be confirmed by the business;
/// a deployment overrides them with <c>INSURANCEINTELLIGENCE_PLAN_CATALOGUE</c> (JSON) without a release.
/// </summary>
public sealed class PlanCatalogue
{
    public const string Free = "free";
    public const string Agent = "agent";
    public const string Team = "team";
    public const string Enterprise = "enterprise";

    /// <summary>The plan of a workspace that has never chosen one: it keeps the limits in its own operating settings, exactly as before plans existed.</summary>
    public const string Standard = "standard";

    public static readonly string[] Currencies = ["UGX", "USD"];

    public PlanCatalogue(IReadOnlyList<Plan> plans) => Plans = plans.OrderBy(plan => plan.Rank).ToArray();

    public IReadOnlyList<Plan> Plans { get; }

    public static PlanCatalogue Default { get; } = new(
    [
        new Plan(Free, "Free", "Try IIP on a few real claims.", 0, true,
            [new PlanPrice("UGX", 0), new PlanPrice("USD", 0)],
            new PlanAllowances(ClaimsPerMonth: 10, DocumentsPerMonth: 100, AiTokensPerMonth: 200_000, StorageMb: 500, Seats: 2)),
        new Plan(Agent, "Agent", "One claims desk, with the assistant on WhatsApp and email.", 1, true,
            [new PlanPrice("UGX", 75_000), new PlanPrice("USD", 20)],
            new PlanAllowances(ClaimsPerMonth: 100, DocumentsPerMonth: 1_000, AiTokensPerMonth: 2_000_000, StorageMb: 5_000, Seats: 3)),
        new Plan(Team, "Team", "A claims team with approvals, products and reports.", 2, true,
            [new PlanPrice("UGX", 370_000), new PlanPrice("USD", 99)],
            new PlanAllowances(ClaimsPerMonth: 600, DocumentsPerMonth: 6_000, AiTokensPerMonth: 15_000_000, StorageMb: 50_000, Seats: 15)),
        new Plan(Enterprise, "Enterprise", "Higher volumes, your own terms and support. Agreed with us directly.", 3, false,
            [],
            PlanAllowances.Unlimited),
    ]);

    public Plan? Find(string? key) =>
        Plans.FirstOrDefault(plan => string.Equals(plan.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The plan a workspace with no subscription is on. It sets no limits of its own.</summary>
    public static Plan StandardPlan { get; } = new(Standard, "Standard", "This workspace keeps the limits in its operating settings.", -1, false, [], PlanAllowances.Unlimited);

    /// <summary>
    /// Reads <c>INSURANCEINTELLIGENCE_PLAN_CATALOGUE</c>: JSON such as
    /// [{"key":"agent","name":"Agent","summary":"...","rank":1,"selfServe":true,"prices":{"UGX":75000,"USD":20},
    ///   "allowances":{"claimsPerMonth":100,"documentsPerMonth":1000,"aiTokensPerMonth":2000000,"storageMb":5000,"seats":3}}].
    /// Anything malformed, or a catalogue with no free plan, falls back to the defaults so a typo cannot leave workspaces with nothing to be on.
    /// </summary>
    public static PlanCatalogue Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Default;
            }

            var plans = new List<Plan>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Text(item, "key") is not { Length: > 0 } key)
                {
                    return Default;
                }

                var prices = new List<PlanPrice>();
                if (item.TryGetProperty("prices", out var priceElement) && priceElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var price in priceElement.EnumerateObject())
                    {
                        if (price.Value.ValueKind != JsonValueKind.Number || !price.Value.TryGetDecimal(out var amount) || amount < 0)
                        {
                            return Default;
                        }

                        prices.Add(new PlanPrice(price.Name.ToUpperInvariant(), amount));
                    }
                }

                var allowances = item.TryGetProperty("allowances", out var allowanceElement) && allowanceElement.ValueKind == JsonValueKind.Object
                    ? new PlanAllowances(
                        Count(allowanceElement, "claimsPerMonth"),
                        Count(allowanceElement, "documentsPerMonth"),
                        Count(allowanceElement, "aiTokensPerMonth"),
                        Count(allowanceElement, "storageMb"),
                        Count(allowanceElement, "seats"))
                    : PlanAllowances.Unlimited;
                plans.Add(new Plan(
                    key.ToLowerInvariant(),
                    Text(item, "name") ?? key,
                    Text(item, "summary") ?? string.Empty,
                    item.TryGetProperty("rank", out var rank) && rank.ValueKind == JsonValueKind.Number && rank.TryGetInt32(out var rankValue) ? rankValue : plans.Count,
                    !item.TryGetProperty("selfServe", out var selfServe) || selfServe.ValueKind != JsonValueKind.False,
                    prices,
                    allowances));
            }

            var catalogue = new PlanCatalogue(plans);
            return plans.Select(plan => plan.Key).Distinct().Count() == plans.Count && catalogue.Find(Free) is { IsFree: true } ? catalogue : Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    private static int Count(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : 0;
}

public static class SubscriptionStatuses
{
    public const string Active = "Active";

    /// <summary>The paid period ended and the renewal has not been paid; the plan still applies for a short grace.</summary>
    public const string PastDue = "PastDue";

    /// <summary>The grace ended unpaid, or the plan was cancelled: the workspace is on the Free plan's limits. Nothing is deleted.</summary>
    public const string Lapsed = "Lapsed";
}

/// <summary>
/// A workspace's plan. One per workspace; a workspace with none is on the Standard plan (see <see cref="PlanCatalogue.Standard"/>).
/// </summary>
public sealed record Subscription
{
    public required TenantScope Scope { get; init; }
    public required string PlanKey { get; init; }
    public string Status { get; init; } = SubscriptionStatuses.Active;
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>The end of the period already paid for. Null on the Free plan, which has no period.</summary>
    public DateTimeOffset? PeriodEnd { get; init; }

    public string Currency { get; init; } = "UGX";

    /// <summary>When set, the plan ends at <see cref="PeriodEnd"/> and the workspace moves to Free instead of renewing.</summary>
    public bool CancelAtPeriodEnd { get; init; }

    /// <summary>When an unpaid renewal stops being tolerated.</summary>
    public DateTimeOffset? GraceEndsAt { get; init; }

    public Guid? OpenRenewalInvoiceId { get; init; }

    /// <summary>Where renewal notices and receipts go: the address of the administrator who last paid.</summary>
    public string? BillingEmail { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public static Subscription Free(TenantScope scope, DateTimeOffset now) =>
        new() { Scope = scope, PlanKey = PlanCatalogue.Free, StartedAt = now, UpdatedAt = now };
}

/// <summary>What a workspace may do right now, after its plan, its status and its own operating settings are taken together.</summary>
public sealed record Entitlements(
    Plan Plan,
    string Status,
    DateTimeOffset? PeriodEnd,
    bool CancelAtPeriodEnd,
    int ClaimsPerMonth,
    int DocumentsPerMonth,
    long AiTokensPerMonth,
    int StorageMb,
    int Seats);

public static class EntitlementRules
{
    /// <summary>
    /// A plan's limit is a ceiling the workspace's own operating setting can lower but never raise. A plan that sets no limit
    /// defers to the setting (this is how a workspace on Standard or Enterprise keeps today's behaviour).
    /// </summary>
    public static int Ceiling(int planLimit, int settingLimit) =>
        planLimit <= 0 ? settingLimit : settingLimit <= 0 ? planLimit : Math.Min(planLimit, settingLimit);

    public static Entitlements For(Subscription? subscription, PlanCatalogue catalogue, int seatSetting, int claimsSetting)
    {
        if (subscription is null)
        {
            return new Entitlements(PlanCatalogue.StandardPlan, SubscriptionStatuses.Active, null, false, claimsSetting, 0, 0, 0, seatSetting);
        }

        // A lapsed workspace is held to the Free plan until it pays; nothing is deleted.
        var effectiveKey = subscription.Status == SubscriptionStatuses.Lapsed ? PlanCatalogue.Free : subscription.PlanKey;
        var plan = catalogue.Find(effectiveKey) ?? catalogue.Find(PlanCatalogue.Free) ?? PlanCatalogue.StandardPlan;
        var allowances = plan.Allowances;
        return new Entitlements(
            plan,
            subscription.Status,
            subscription.PeriodEnd,
            subscription.CancelAtPeriodEnd,
            Ceiling(allowances.ClaimsPerMonth, claimsSetting),
            allowances.DocumentsPerMonth,
            allowances.AiTokensPerMonth,
            allowances.StorageMb,
            Ceiling(allowances.Seats, seatSetting));
    }

    /// <summary>The next plan up that a workspace can buy for itself, or null when there is none.</summary>
    public static Plan? NextUp(PlanCatalogue catalogue, Plan current) =>
        catalogue.Plans.Where(plan => plan.SelfServe && !plan.IsFree && plan.Rank > current.Rank).OrderBy(plan => plan.Rank).FirstOrDefault();
}

/// <summary>Thrown when a plan's allowance stops an action. The message is written for the staff member who will read it.</summary>
public sealed class PlanLimitExceededException(string meter, string planName, long limit, long used, string? upgradeToPlanKey)
    : InvalidOperationException(BuildMessage(meter, planName, limit))
{
    public string Meter { get; } = meter;
    public string PlanName { get; } = planName;
    public long Limit { get; } = limit;
    public long Used { get; } = used;
    public string? UpgradeToPlanKey { get; } = upgradeToPlanKey;

    private static string BuildMessage(string meter, string planName, long limit) => meter switch
    {
        "claims" => $"This month's allowance of {limit:N0} claims on the {planName} plan has been used. Upgrade the plan to open more.",
        "ai-tokens" => $"This month's AI allowance on the {planName} plan has been used. Upgrade the plan to keep using the assistant.",
        _ => $"The {planName} plan's allowance for {meter} has been used. Upgrade the plan to continue.",
    };
}

/// <summary>Checked before something that uses a metered allowance happens. Implementations throw <see cref="PlanLimitExceededException"/>.</summary>
public interface IPlanAllowance
{
    Task EnsureCanOpenClaimAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task EnsureCanUseAiAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

public interface ISubscriptionStore
{
    Task<Subscription?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(Subscription subscription, CancellationToken cancellationToken = default);

    /// <summary>Paid subscriptions whose paid period ends before the moment given, across all workspaces. Only the platform's own timer calls this.</summary>
    Task<IReadOnlyList<Subscription>> ListPaidEndingBeforeAsync(DateTimeOffset moment, CancellationToken cancellationToken = default);
}
