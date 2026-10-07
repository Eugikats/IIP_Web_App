using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Fraud;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Fraud;

/// <summary>
/// Surfaces deterministic, rule-based fraud AND AML/CFT advisory signals (blueprint §14 Phase 7,
/// WO 7.3) for the current tenant/workspace. Reads real claims (tenant-scoped by the claims read
/// repository) and runs the pure <see cref="FraudRuleEngine"/>. Produces advisory
/// signals only, never a verdict (DEC-014, AGENTS.md decision boundary).
/// </summary>
public sealed class FraudQueryService(
    IClaimsReadRepository claimsReadRepository,
    IPolicyRepository policyRepository,
    IPermissionService permissionService,
    ITenantContextAccessor tenantContextAccessor,
    IFraudSignalDispositionRepository dispositionRepository) : IFraudQueryService
{
    // Analyse a bounded, recent window of claims. Fraud patterns are cross-claim,
    // so we pull a generous page rather than a single-claim view.
    private const int AnalysisWindow = 500;

    public async Task<IReadOnlyList<FraudSignalDto>> GetSignalsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewFraudSignals, cancellationToken);

        var scope = tenantContextAccessor.Current.Scope;
        var claims = await claimsReadRepository.ListAsync(new ClaimsQuery(Take: AnalysisWindow), cancellationToken);
        // AML/CFT rules need the policy's own EffectiveFrom/SumInsured (WO 7.3), real policy data,
        // not fabricated; a claim whose policy number doesn't resolve (or never existed) simply
        // carries null for those two fields, and the two AML/CFT rules honestly skip it rather than
        // guessing. The workspace's policies are read once: one lookup per claim was about 300 SQL
        // round trips per request on dev (7 seconds).
        var policiesByNumber = (await policyRepository.ListAsync(scope, cancellationToken))
            .Where(policy => !string.IsNullOrWhiteSpace(policy.PolicyNumber))
            .GroupBy(policy => policy.PolicyNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var inputs = claims
            .Select(claim => ToFraudInput(
                claim,
                string.IsNullOrWhiteSpace(claim.PolicyNumber) ? null : policiesByNumber.GetValueOrDefault(claim.PolicyNumber.Trim())))
            .ToList();

        var signals = FraudRuleEngine.Evaluate(inputs);
        // WO-19: signals are recomputed fresh every call, join each one to its persisted
        // disposition (if any) by the same deterministic id the disposition service uses, so the
        // workflow state survives even though the signal itself is never stored.
        var dispositions = (await dispositionRepository.ListAsync(scope, cancellationToken))
            .ToDictionary(d => d.SignalId);

        return signals.Select(signal => ToDto(signal, dispositions)).ToArray();
    }

    private static FraudClaimInput ToFraudInput(ClaimCase claim, Policy? policy) =>
        new(
            claim.CaseId,
            claim.CustomerName,
            claim.CustomerEmail,
            claim.CustomerPhone,
            claim.PolicyNumber,
            claim.ClaimType,
            // ClaimCase.CompletenessScore is a 0–100 percentage; the engine expects 0–1.
            claim.CompletenessScore / 100m,
            claim.CreatedAt,
            PolicyEffectiveFrom: policy?.EffectiveFrom,
            PolicySumInsured: policy?.SumInsured);

    private static FraudSignalDto ToDto(
        FraudAdvisorySignal signal, IReadOnlyDictionary<Guid, FraudSignalDisposition> dispositions)
    {
        var signalId = FraudSignalDisposition.DeterministicId(signal.RuleId, signal.RelatedCaseIds);
        var disposition = dispositions.TryGetValue(signalId, out var found) ? found : null;
        return new(
            signalId,
            signal.RuleId,
            signal.SignalType,
            signal.Title,
            signal.Description,
            signal.Severity.ToString(),
            signal.RelatedCaseIds.Select(id => id.ToString()).ToArray(),
            signal.Evidence,
            disposition is null
                ? new FraudDispositionDto(
                    "NotTriaged", null, null, null, string.Empty, default, default,
                    FraudDispositionMapper.AllowedForUntriaged(), FraudDispositionMapper.BlockedForUntriaged())
                : FraudDispositionMapper.ToDto(disposition));
    }
}
