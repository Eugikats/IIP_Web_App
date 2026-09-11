using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Cases;

/// <summary>
/// Step 2 of the six-step FNOL intake (docs/03-design-architecture/iip_fnol_six_step_intake_design.md):
/// resolve a policy number into real, validated context — policyholder, product line, cover status —
/// before the claimant is asked another question, and flag a possible duplicate claim on the same
/// policy so it is caught here rather than at review. Deliberately read-only: intake still creates the
/// claim through the existing CaseEndpoints.CreateCase path, this only informs what that call sends.
///
/// Resolves the policy through IPolicyLookupProvider (blueprint §14 Phase 7, WO 7.1) rather than
/// ICustomerRepository/IPolicyRepository directly — this is the platform's real integration seam
/// for an insurer's policy administration system: once a tenant connects a real PAS via a
/// PolicySystem Connection (WO 7.2), this endpoint resolves against it with no code change here.
/// </summary>
public static class PolicyLookupEndpoints
{
    // Terminal states only; every other status is an "open" claim for duplicate-detection purposes.
    private static readonly HashSet<CaseStatus> TerminalStatuses = [CaseStatus.Settled, CaseStatus.Closed];

    public static async Task<IResult> LookupByPolicyNumber(
        [FromQuery] string? policyNumber,
        IPolicyLookupProvider policyLookupProvider,
        ICustomerRepository customerRepository,
        ICaseRepository caseRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["policyNumber"] = ["A policy number is required to look up policy context."]
            });
        }

        var policy = await policyLookupProvider.FindPolicyByNumberAsync(policyNumber.Trim(), scope, cancellationToken);
        if (policy is null)
        {
            return Results.NotFound(new { message = $"No policy found for number \"{policyNumber.Trim()}\" in this workspace." });
        }

        var customer = string.IsNullOrWhiteSpace(policy.CustomerId)
            ? null
            : await customerRepository.GetAsync(policy.CustomerId, scope, cancellationToken);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var isActive = (policy.EffectiveFrom is null || policy.EffectiveFrom.Value <= today)
            && (policy.EffectiveTo is null || policy.EffectiveTo.Value >= today);

        var allCases = await caseRepository.ListAsync(scope, cancellationToken);
        var possibleDuplicates = allCases
            .Where(c => string.Equals(c.PolicyNumber, policy.PolicyNumber, StringComparison.OrdinalIgnoreCase)
                && !TerminalStatuses.Contains(c.Status))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new DuplicateClaimWarning(c.CaseId, c.Status.ToString(), c.IncidentDate, c.CreatedAt))
            .ToArray();

        return Results.Ok(new PolicyLookupResponse(
            policy.PolicyId,
            policy.PolicyNumber,
            policy.ProductLine,
            policy.CoverType,
            customer?.FullName,
            customer?.Email,
            customer?.Phone,
            policy.EffectiveFrom,
            policy.EffectiveTo,
            isActive,
            possibleDuplicates));
    }
}

public sealed record PolicyLookupResponse(
    string PolicyId,
    string PolicyNumber,
    string ProductLine,
    string? CoverType,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    IReadOnlyCollection<DuplicateClaimWarning> PossibleDuplicateClaims);

public sealed record DuplicateClaimWarning(Guid CaseId, string Status, DateOnly? IncidentDate, DateTimeOffset CreatedAt);
