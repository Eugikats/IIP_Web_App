using InsuranceIntelligence.Domain.Underwriting;

namespace InsuranceIntelligence.Application.Underwriting;

public interface IUnderwritingQueryService
{
    Task<IReadOnlyList<UnderwritingReviewDto>> GetReviewsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Port for the underwriting submission intake. Until a real broker/direct
/// submission source is configured, the bound implementation returns none, and
/// the screen shows an honest "not configured" empty state (no fabricated queue).
/// </summary>
public interface IUnderwritingSubmissionSource
{
    Task<IReadOnlyList<UnderwritingSubmissionInput>> GetPendingSubmissionsAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed record UnderwritingReviewDto(
    string SubmissionId,
    string Applicant,
    IReadOnlyList<UnderwritingCheckDto> Checks,
    IReadOnlyList<string> DraftedQuestions);

public sealed record UnderwritingCheckDto(
    string CheckId,
    string Label,
    string State,
    string Detail);
