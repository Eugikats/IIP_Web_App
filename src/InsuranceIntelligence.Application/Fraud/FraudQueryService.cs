using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Application.Fraud;

/// <summary>
/// Surfaces deterministic, rule-based fraud advisory signals for the current
/// tenant/workspace. Reads real claims (tenant-scoped by the claims read
/// repository) and runs the pure <see cref="FraudRuleEngine"/>. Produces advisory
/// signals only — never a verdict (DEC-014, AGENTS.md decision boundary).
/// </summary>
public sealed class FraudQueryService(
    IClaimsReadRepository claimsReadRepository,
    IPermissionService permissionService) : IFraudQueryService
{
    // Analyse a bounded, recent window of claims. Fraud patterns are cross-claim,
    // so we pull a generous page rather than a single-claim view.
    private const int AnalysisWindow = 500;

    public async Task<IReadOnlyList<FraudSignalDto>> GetSignalsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewFraudSignals, cancellationToken);

        var claims = await claimsReadRepository.ListAsync(new ClaimsQuery(Take: AnalysisWindow), cancellationToken);
        var inputs = claims.Select(ToFraudInput).ToArray();

        var signals = FraudRuleEngine.Evaluate(inputs);
        return signals.Select(ToDto).ToArray();
    }

    private static FraudClaimInput ToFraudInput(ClaimCase claim) =>
        new(
            claim.CaseId,
            claim.CustomerName,
            claim.CustomerEmail,
            claim.CustomerPhone,
            claim.PolicyNumber,
            claim.ClaimType,
            // ClaimCase.CompletenessScore is a 0–100 percentage; the engine expects 0–1.
            claim.CompletenessScore / 100m,
            claim.CreatedAt);

    private static FraudSignalDto ToDto(FraudAdvisorySignal signal) =>
        new(
            signal.RuleId,
            signal.SignalType,
            signal.Title,
            signal.Description,
            signal.Severity.ToString(),
            signal.RelatedCaseIds.Select(id => id.ToString()).ToArray(),
            signal.Evidence);
}
