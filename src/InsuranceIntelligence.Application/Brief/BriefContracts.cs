namespace InsuranceIntelligence.Application.Brief;

public sealed record BriefItemDto(
    string CaseId,
    string CustomerName,
    string PolicyNumber,
    string ClaimType,
    string Status,
    string Reason,
    string PrimaryActionLabel,
    string PrimaryActionHref);

public sealed record HandledForYouItemDto(
    string? CaseId,
    string Summary,
    DateTimeOffset HandledAt);

public sealed record BriefResponse(
    IReadOnlyList<BriefItemDto> Items,
    IReadOnlyList<HandledForYouItemDto> HandledForYou,
    // Outcome O: "a line in the brief showing premium at risk." Null when the retention watch
    // service is unavailable, distinct from a real zero.
    BriefRetentionSummaryDto? Retention = null);

public sealed record BriefRetentionSummaryDto(int PoliciesAtRisk, decimal PremiumAtRisk, string PremiumAtRiskCurrency);

public interface IBriefService
{
    Task<BriefResponse> GetAsync(CancellationToken cancellationToken = default);
}
