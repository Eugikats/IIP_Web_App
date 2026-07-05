using InsuranceIntelligence.Application.Underwriting;
using InsuranceIntelligence.Domain.Underwriting;

namespace InsuranceIntelligence.Api.Underwriting;

public static class UnderwritingEndpoints
{
    public static async Task<IResult> GetReviews(
        IUnderwritingQueryService queryService,
        CancellationToken cancellationToken)
    {
        var reviews = await queryService.GetReviewsAsync(cancellationToken);
        return Results.Ok(reviews);
    }
}

/// <summary>
/// Honest default submission source: no broker/direct submission intake is wired,
/// so no submissions are returned and the Underwriting screen shows an empty
/// "not configured" state rather than fabricated queue entries. Replace with a
/// real intake adapter when a submissions channel is configured.
/// </summary>
public sealed class NotConfiguredUnderwritingSubmissionSource : IUnderwritingSubmissionSource
{
    public Task<IReadOnlyList<UnderwritingSubmissionInput>> GetPendingSubmissionsAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UnderwritingSubmissionInput>>([]);
}
