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
    IReadOnlyList<HandledForYouItemDto> HandledForYou);

public interface IBriefService
{
    Task<BriefResponse> GetAsync(CancellationToken cancellationToken = default);
}
