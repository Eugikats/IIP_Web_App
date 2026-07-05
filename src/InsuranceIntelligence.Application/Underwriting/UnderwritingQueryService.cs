using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Underwriting;

namespace InsuranceIntelligence.Application.Underwriting;

/// <summary>
/// Runs the deterministic <see cref="UnderwritingRuleEngine"/> over the tenant's
/// pending submissions and returns advisory reviews. Never decides
/// acceptance/rejection/premium (DEC-014). Returns an empty list honestly when no
/// submission source is configured.
/// </summary>
public sealed class UnderwritingQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IUnderwritingSubmissionSource submissionSource) : IUnderwritingQueryService
{
    public async Task<IReadOnlyList<UnderwritingReviewDto>> GetReviewsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewUnderwriting, cancellationToken);

        var context = tenantContextAccessor.Current;
        var submissions = await submissionSource.GetPendingSubmissionsAsync(context.TenantId, context.WorkspaceId, cancellationToken);

        var reviews = UnderwritingRuleEngine.Review(submissions);
        return reviews.Select(ToDto).ToArray();
    }

    private static UnderwritingReviewDto ToDto(UnderwritingReview review) =>
        new(
            review.SubmissionId,
            review.Applicant,
            review.Checks
                .Select(c => new UnderwritingCheckDto(c.CheckId, c.Label, c.State.ToString(), c.Detail))
                .ToArray(),
            review.DraftedQuestions);
}
