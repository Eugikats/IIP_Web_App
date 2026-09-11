namespace InsuranceIntelligence.Domain.Fraud;

/// <summary>
/// Advisory severity for a fraud signal. The platform never asserts confirmed
/// fraud (AGENTS.md human-supervised decision boundary, DEC-014); the strongest
/// output is "requires investigation". A human investigator decides.
/// </summary>
public enum FraudSignalSeverity
{
    AdvisorySignal,
    PotentialAnomaly,
    RequiresInvestigation
}

/// <summary>
/// Minimal, decoupled projection of a claim used to evaluate fraud rules. Kept
/// free of persistence types so the engine is a pure, unit-testable function.
/// </summary>
public sealed record FraudClaimInput(
    Guid CaseId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string PolicyNumber,
    string ClaimType,
    decimal CompletenessScore,
    DateTimeOffset CreatedAt,
    /// <summary>The policy's own effective-from date (blueprint §14 Phase 7, WO 7.3) — null when
    /// the policy record wasn't found or never set one. Powers the early-claim AML/CFT rule; not
    /// used by any fraud rule.</summary>
    DateOnly? PolicyEffectiveFrom = null,
    /// <summary>The policy's sum insured (WO 7.3) — null when the policy record wasn't found or
    /// never set one. Powers the high-value AML/CFT rule; not used by any fraud rule.</summary>
    decimal? PolicySumInsured = null);

/// <summary>
/// A deterministic advisory signal. Always carries the rule that produced it and
/// the evidence that triggered it, so a human can verify it. Never a verdict.
/// </summary>
public sealed record FraudAdvisorySignal(
    string RuleId,
    string SignalType,
    string Title,
    string Description,
    FraudSignalSeverity Severity,
    IReadOnlyList<Guid> RelatedCaseIds,
    IReadOnlyList<string> Evidence);

public sealed record FraudRuleOptions
{
    public int VelocityWindowDays { get; init; } = 30;
    public int VelocityClaimCount { get; init; } = 3;
    public decimal LowCompletenessThreshold { get; init; } = 0.40m;

    /// <summary>WO 7.3, AML/CFT: a claim filed within this many days of the policy's own
    /// effective-from date is flagged — a policy taken out specifically to stage a loss and
    /// extract a payout is a recognized laundering vector, distinct from ordinary claims fraud.</summary>
    public int EarlyClaimWindowDays { get; init; } = 14;

    /// <summary>WO 7.3, AML/CFT: a policy's sum insured above this threshold is flagged on any
    /// claim against it — large-value transactions warrant AML/CFT review independent of any
    /// fraud indicator. UGX-denominated, matching this platform's demo-seed premium bands.</summary>
    public decimal HighValueSumInsuredThreshold { get; init; } = 100_000_000m;

    public static readonly FraudRuleOptions Default = new();
}

/// <summary>
/// Deterministic, rule-based fraud advisory engine (v1). Produces auditable
/// advisory signals from real claim data — no ML, no LLM, no verdicts. A future
/// Azure OpenAI narration layer may explain these signals at Gate 3, but must not
/// replace this engine (memory: fraud-underwriting-llm-narration-followup).
/// </summary>
public static class FraudRuleEngine
{
    public static IReadOnlyList<FraudAdvisorySignal> Evaluate(
        IReadOnlyList<FraudClaimInput> claims,
        FraudRuleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(claims);
        options ??= FraudRuleOptions.Default;

        var signals = new List<FraudAdvisorySignal>();
        signals.AddRange(DuplicateContactSignals(claims));
        signals.AddRange(ClaimVelocitySignals(claims, options));
        signals.AddRange(DuplicatePolicySignals(claims));
        signals.AddRange(LowCompletenessSignals(claims, options));
        signals.AddRange(EarlyClaimSignals(claims, options));
        signals.AddRange(HighValueSignals(claims, options));
        return signals;
    }

    // R-AML-EARLY-CLAIM (WO 7.3, advisory only — never a determination): a claim filed shortly
    // after the policy's own effective-from date. A policy taken out specifically to stage a loss
    // is a recognized AML/CFT laundering vector, distinct from ordinary claims fraud (which this
    // engine's other rules already cover) — flagged here even when nothing else about the claim
    // looks anomalous.
    private static IEnumerable<FraudAdvisorySignal> EarlyClaimSignals(IReadOnlyList<FraudClaimInput> claims, FraudRuleOptions options)
    {
        foreach (var claim in claims.Where(c => c.PolicyEffectiveFrom is not null))
        {
            var incidentDate = DateOnly.FromDateTime(claim.CreatedAt.UtcDateTime);
            var daysSinceInception = incidentDate.DayNumber - claim.PolicyEffectiveFrom!.Value.DayNumber;
            if (daysSinceInception < 0 || daysSinceInception > options.EarlyClaimWindowDays)
            {
                continue;
            }

            yield return new FraudAdvisorySignal(
                RuleId: "R-AML-EARLY-CLAIM",
                SignalType: "AML/CFT: early claim after policy inception",
                Title: $"Claim {claim.CaseId} filed {daysSinceInception} day(s) after policy inception",
                Description: $"AML/CFT advisory signal, not a determination: this claim was filed {daysSinceInception} day(s) after policy {claim.PolicyNumber}'s effective date — within the {options.EarlyClaimWindowDays}-day review window. Confirm the loss timeline before proceeding; this alone does not establish wrongdoing.",
                Severity: FraudSignalSeverity.PotentialAnomaly,
                RelatedCaseIds: [claim.CaseId],
                Evidence: [$"Policy effective from: {claim.PolicyEffectiveFrom:yyyy-MM-dd}", $"Claim filed: {incidentDate:yyyy-MM-dd}", $"Days since inception: {daysSinceInception}"]);
        }
    }

    // R-AML-HIGH-VALUE (WO 7.3, advisory only — never a determination): the policy's own sum
    // insured exceeds the configured threshold. Large-value transactions warrant AML/CFT review
    // independent of any fraud indicator — this rule fires on value alone, deliberately.
    private static IEnumerable<FraudAdvisorySignal> HighValueSignals(IReadOnlyList<FraudClaimInput> claims, FraudRuleOptions options)
    {
        foreach (var claim in claims.Where(c => c.PolicySumInsured is not null && c.PolicySumInsured.Value >= options.HighValueSumInsuredThreshold))
        {
            yield return new FraudAdvisorySignal(
                RuleId: "R-AML-HIGH-VALUE",
                SignalType: "AML/CFT: high-value policy claim",
                Title: $"Claim {claim.CaseId} is against a high sum-insured policy",
                Description: $"AML/CFT advisory signal, not a determination: policy {claim.PolicyNumber}'s sum insured ({claim.PolicySumInsured:N0}) meets or exceeds the {options.HighValueSumInsuredThreshold:N0} review threshold. Large-value transactions require AML/CFT review regardless of any other indicator.",
                Severity: FraudSignalSeverity.AdvisorySignal,
                RelatedCaseIds: [claim.CaseId],
                Evidence: [$"Sum insured: {claim.PolicySumInsured:N0}", $"Threshold: {options.HighValueSumInsuredThreshold:N0}", $"Policy: {claim.PolicyNumber}"]);
        }
    }

    // R-DUP-CONTACT: the same phone or email appears on more than one distinct claim.
    private static IEnumerable<FraudAdvisorySignal> DuplicateContactSignals(IReadOnlyList<FraudClaimInput> claims)
    {
        foreach (var group in claims
            .SelectMany(ContactKeys, (claim, key) => (key, claim))
            .GroupBy(x => x.key.Value, StringComparer.Ordinal))
        {
            var distinctCases = group.Select(x => x.claim).DistinctBy(c => c.CaseId).ToArray();
            if (distinctCases.Length < 2)
            {
                continue;
            }

            var kind = group.First().key.Kind;
            yield return new FraudAdvisorySignal(
                RuleId: "R-DUP-CONTACT",
                SignalType: "Shared contact across claims",
                Title: $"{distinctCases.Length} claims share the same {kind}",
                Description: $"Advisory signal: {distinctCases.Length} distinct claims are linked to the same {kind}. This may be legitimate (a household or fleet) or may warrant a closer look — it is not an accusation.",
                Severity: FraudSignalSeverity.PotentialAnomaly,
                RelatedCaseIds: distinctCases.Select(c => c.CaseId).ToArray(),
                Evidence: new[] { $"{kind}: {group.Key}" }
                    .Concat(distinctCases.Select(c => $"Claim {c.CaseId} · {c.CustomerName}"))
                    .ToArray());
        }
    }

    // R-VELOCITY: one claimant files many claims within a short window.
    private static IEnumerable<FraudAdvisorySignal> ClaimVelocitySignals(IReadOnlyList<FraudClaimInput> claims, FraudRuleOptions options)
    {
        foreach (var group in claims.GroupBy(ClaimantKey, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(c => c.CreatedAt).ToArray();
            for (var i = 0; i + options.VelocityClaimCount - 1 < ordered.Length; i++)
            {
                var window = ordered.Skip(i).Take(options.VelocityClaimCount).ToArray();
                var span = window[^1].CreatedAt - window[0].CreatedAt;
                if (span.TotalDays <= options.VelocityWindowDays)
                {
                    yield return new FraudAdvisorySignal(
                        RuleId: "R-VELOCITY",
                        SignalType: "High claim velocity",
                        Title: $"{window.Length} claims from one claimant within {options.VelocityWindowDays} days",
                        Description: $"Requires investigation: {group.First().CustomerName} filed {window.Length} claims within {options.VelocityWindowDays} days. Confirm each is a distinct, genuine incident.",
                        Severity: FraudSignalSeverity.RequiresInvestigation,
                        RelatedCaseIds: window.Select(c => c.CaseId).ToArray(),
                        Evidence: window.Select(c => $"Claim {c.CaseId} · {c.ClaimType} · {c.CreatedAt:yyyy-MM-dd}").ToArray());
                    break; // one velocity signal per claimant is enough
                }
            }
        }
    }

    // R-DUP-POLICY: one policy number is claimed by more than one distinct claimant name.
    private static IEnumerable<FraudAdvisorySignal> DuplicatePolicySignals(IReadOnlyList<FraudClaimInput> claims)
    {
        foreach (var group in claims
            .Where(c => !string.IsNullOrWhiteSpace(c.PolicyNumber))
            .GroupBy(c => c.PolicyNumber.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var names = group.Select(c => NormalizeName(c.CustomerName)).Distinct(StringComparer.Ordinal).ToArray();
            if (names.Length < 2)
            {
                continue;
            }

            yield return new FraudAdvisorySignal(
                RuleId: "R-DUP-POLICY",
                SignalType: "Policy linked to multiple claimants",
                Title: $"Policy {group.Key} appears under {names.Length} claimant names",
                Description: $"Potential anomaly: policy {group.Key} is associated with {names.Length} different claimant names across claims. Verify the policyholder relationship before proceeding.",
                Severity: FraudSignalSeverity.PotentialAnomaly,
                RelatedCaseIds: group.Select(c => c.CaseId).ToArray(),
                Evidence: group.Select(c => $"Claim {c.CaseId} · {c.CustomerName}").ToArray());
        }
    }

    // R-LOW-COMPLETENESS: an advisory nudge for claims with very low documentation completeness.
    private static IEnumerable<FraudAdvisorySignal> LowCompletenessSignals(IReadOnlyList<FraudClaimInput> claims, FraudRuleOptions options)
    {
        foreach (var claim in claims.Where(c => c.CompletenessScore < options.LowCompletenessThreshold))
        {
            yield return new FraudAdvisorySignal(
                RuleId: "R-LOW-COMPLETENESS",
                SignalType: "Low documentation completeness",
                Title: $"Claim {claim.CaseId} has low documentation completeness",
                Description: $"Advisory signal: documentation completeness is {claim.CompletenessScore:P0}, below the {options.LowCompletenessThreshold:P0} threshold. Low completeness alone is not suspicious — request the outstanding evidence before assessing.",
                Severity: FraudSignalSeverity.AdvisorySignal,
                RelatedCaseIds: new[] { claim.CaseId },
                Evidence: new[] { $"Completeness: {claim.CompletenessScore:P0}", $"Claim type: {claim.ClaimType}" });
        }
    }

    private static IEnumerable<ContactKey> ContactKeys(FraudClaimInput claim)
    {
        var phone = NormalizePhone(claim.CustomerPhone);
        if (phone is not null)
        {
            yield return new ContactKey("phone number", phone);
        }

        var email = NormalizeEmail(claim.CustomerEmail);
        if (email is not null)
        {
            yield return new ContactKey("email", email);
        }
    }

    private static string ClaimantKey(FraudClaimInput claim)
    {
        var phone = NormalizePhone(claim.CustomerPhone);
        return phone ?? NormalizeName(claim.CustomerName);
    }

    private static string NormalizeName(string value) =>
        string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits : null;
    }

    private static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private readonly record struct ContactKey(string Kind, string Value);
}
