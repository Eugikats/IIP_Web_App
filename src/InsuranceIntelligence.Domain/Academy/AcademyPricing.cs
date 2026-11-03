using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Academy;

/// <summary>What one IIP Academy course costs a learner in this workspace.</summary>
public sealed record CoursePrice(string CourseId, decimal Amount, string Currency = AcademyPricing.Currency);

/// <summary>
/// The prices a workspace charges for IIP Academy courses. A course with no saved price costs the seed
/// price, so a new workspace starts with every course listed at the same amount and can change each one
/// on the Academy team screen. Prices are shown to learners and are what the payment is worked out from.
/// </summary>
public sealed record AcademyPricing(TenantScope Scope, IReadOnlyList<CoursePrice> Prices, DateTimeOffset? UpdatedAt = null, string? UpdatedBy = null)
{
    /// <summary>Prices are in US dollars. A price saved before that change was in shillings and is not carried over as dollars.</summary>
    public const string Currency = "USD";
    public const decimal SeedPrice = 120m;
    public const decimal MaxPrice = 10_000m;

    /// <summary>The courses IIP Academy offers. A price for any other id is rejected.</summary>
    public static readonly IReadOnlyList<string> CourseIds =
    [
        "ai-agentic-insurance", "ai-claims-fraud", "ai-underwriting-risk", "ai-sales-distribution", "agentic-systems",
        "claims", "underwriting", "fraud-identity", "governance", "claims-communication",
    ];

    public static AcademyPricing Seed(TenantScope scope) =>
        new(scope, CourseIds.Select(id => new CoursePrice(id, DefaultPrice(id))).ToArray());

    public static decimal DefaultPrice(string courseId) => SeedPrice;

    /// <summary>Every known course with its saved price, or the seed price where none was saved.</summary>
    public AcademyPricing WithDefaults() =>
        this with
        {
            Prices = CourseIds
                .Select(id => Prices.FirstOrDefault(price => string.Equals(price.CourseId, id, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(price.Currency, Currency, StringComparison.OrdinalIgnoreCase)) ?? new CoursePrice(id, DefaultPrice(id)))
                .ToArray(),
        };

    /// <summary>Why this price is not acceptable, or null when it is.</summary>
    public static string? Problem(CoursePrice price)
    {
        if (!CourseIds.Contains(price.CourseId, StringComparer.OrdinalIgnoreCase))
        {
            return $"'{price.CourseId}' is not a course.";
        }

        if (price.Amount < 0 || price.Amount > MaxPrice)
        {
            return $"A price must be between 0 and {MaxPrice:N0}.";
        }

        if (decimal.Round(price.Amount, 2) != price.Amount)
        {
            return "A price can have at most two decimal places.";
        }

        return null;
    }
}

public interface IAcademyPricingRepository
{
    Task<AcademyPricing?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(AcademyPricing pricing, CancellationToken cancellationToken = default);
}
