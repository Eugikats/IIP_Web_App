using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Scheduling;

namespace InsuranceIntelligence.Application.Brief;

/// <summary>
/// Backs the landing brief (build prompt outcome F): at most five ranked claims, each with a
/// one-sentence reason built from real fields (ClaimBriefRanking), plus a short "handled for you"
/// list drawn from the audit trail of what this person already resolved. Honestly empty when there
/// is nothing, rather than showing chips that lead nowhere.
/// </summary>
public sealed class BriefService(
    ICaseRepository caseRepository,
    IAuditRepository auditRepository,
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService) : IBriefService
{
    private static readonly string[] HandledActionSuffixes =
    [
        ".approved", ".sent", ".rejected", ".edited_and_approved", ".confirmed", ".escalated", ".executed",
    ];

    public async Task<BriefResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var viewAll = await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken);
        var viewAssigned = await permissionService.HasPermissionAsync(Permission.ViewAssignedClaims, cancellationToken);
        if (!viewAll && !viewAssigned)
        {
            throw new PermissionDeniedException(Permission.ViewAssignedClaims);
        }

        var context = tenantContextAccessor.Current;
        var allCases = await caseRepository.ListAsync(context.Scope, cancellationToken);
        var myCases = allCases.Where(claimCase => IsMine(claimCase, context.UserId)).ToArray();

        var now = DateTimeOffset.UtcNow;
        var holidays = UgandaPublicHolidays.ForYears(now.Year - 1, now.Year + 1);
        var ranked = ClaimBriefRanking.Rank(myCases, holidays, now);
        var items = ranked.Select(ToDto).ToArray();

        var auditEvents = await auditRepository.ListAsync(context.Scope, cancellationToken);
        var handled = auditEvents
            .Where(auditEvent => string.Equals(auditEvent.UserId, context.UserId, StringComparison.OrdinalIgnoreCase))
            .Where(auditEvent => IsHandledAction(auditEvent.ActionName))
            .Where(auditEvent => auditEvent.Timestamp >= now.AddHours(-24))
            .OrderByDescending(auditEvent => auditEvent.Timestamp)
            .Take(5)
            .Select(auditEvent => new HandledForYouItemDto(
                auditEvent.CaseId?.ToString(),
                Describe(auditEvent.ActionName),
                auditEvent.Timestamp))
            .ToArray();

        return new BriefResponse(items, handled);
    }

    private static bool IsMine(ClaimCase claimCase, string userId) =>
        claimCase.Status is not (CaseStatus.Settled or CaseStatus.Closed)
        && (Matches(claimCase.AssignedUserId, userId) || (string.IsNullOrEmpty(claimCase.AssignedUserId) && Matches(claimCase.CreatedByUserId, userId)));

    private static bool Matches(string? candidate, string userId) =>
        !string.IsNullOrWhiteSpace(candidate) && string.Equals(candidate.Trim(), userId.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsHandledAction(string actionName) =>
        HandledActionSuffixes.Any(suffix => actionName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static string Describe(string actionName)
    {
        var words = actionName.Replace('.', ' ').Replace('_', ' ');
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static BriefItemDto ToDto(ClaimBriefItem item) =>
        new(
            item.ClaimCase.CaseId.ToString(),
            item.ClaimCase.CustomerName,
            item.ClaimCase.PolicyNumber,
            item.ClaimCase.ClaimType,
            item.ClaimCase.Status.ToString(),
            item.Reason,
            item.PrimaryActionLabel,
            item.PrimaryActionHref);
}
