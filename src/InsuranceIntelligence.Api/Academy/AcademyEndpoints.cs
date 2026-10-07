using InsuranceIntelligence.Application.Academy;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy;

public static class AcademyEndpoints
{
    public sealed record PriceRequest(string CourseId, decimal Amount);

    public sealed record SavePricingRequest(IReadOnlyList<PriceRequest>? Prices);

    public static async Task<IResult> GetPricing(IAcademyPricingService pricing, CancellationToken cancellationToken) =>
        Results.Ok(ToResponse(await pricing.GetAsync(cancellationToken)));

    public static async Task<IResult> SavePricing(SavePricingRequest request, IAcademyPricingService pricing, CancellationToken cancellationToken)
    {
        try
        {
            var saved = await pricing.SaveAsync((request.Prices ?? []).Select(price => new CoursePrice(price.CourseId, price.Amount)).ToArray(), cancellationToken);
            return Results.Ok(ToResponse(saved));
        }
        catch (AcademyPriceValidationException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["prices"] = [exception.Message] });
        }
    }

    private static object ToResponse(AcademyPricing pricing) => new
    {
        currency = AcademyPricing.Currency,
        prices = pricing.Prices.Select(price => new { courseId = price.CourseId, amount = price.Amount }),
        updatedAt = pricing.UpdatedAt,
    };
}
