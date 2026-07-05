using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Domain.Billing;

namespace InsuranceIntelligence.Api.Admin.Billing;

public static class BillingEndpoints
{
    public static async Task<IResult> GetSummary(
        IBillingQueryService service,
        CancellationToken cancellationToken)
    {
        var summary = await service.GetSummaryAsync(cancellationToken);
        return Results.Ok(summary);
    }
}

/// <summary>
/// Computes usage from records the platform actually stores. Claims are counted
/// for real via the claims read repository; dimensions without a counter yet
/// (AI calls, storage) are returned Metered=false so the UI shows "Not
/// configured" rather than a fabricated number (ADR-0003).
/// </summary>
public sealed class RecordBackedBillingUsageSource(IClaimsReadRepository claimsReadRepository) : IBillingUsageSource
{
    public async Task<IReadOnlyList<UsageMeterDto>> GetUsageAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var claims = await claimsReadRepository.ListAsync(new ClaimsQuery(Take: 5000), cancellationToken);
        var claimCount = claims.Count;

        var meters = new[]
        {
            new UsageMeter("claims", "Claims processed", claimCount, 5000, "claims", Metered: true),
            new UsageMeter("documents", "Documents processed", 0, 0, "documents", Metered: false),
            new UsageMeter("ai-calls", "AI generation calls", 0, 0, "calls", Metered: false),
            new UsageMeter("storage", "Document storage", 0, 0, "GB", Metered: false),
        };

        return meters.Select(ToDto).ToArray();
    }

    private static UsageMeterDto ToDto(UsageMeter m) =>
        new(m.Key, m.Label, m.Used, m.Limit, m.Unit, m.Metered, m.OverLimit, m.PercentUsed);
}
