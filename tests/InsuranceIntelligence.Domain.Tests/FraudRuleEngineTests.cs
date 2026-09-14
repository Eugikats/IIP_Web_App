using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class FraudRuleEngineTests
{
    private static FraudClaimInput Claim(
        string name,
        string? phone = null,
        string? email = null,
        string policy = "POL-1",
        string claimType = "Motor",
        decimal completeness = 0.9m,
        DateTimeOffset? createdAt = null,
        Guid? caseId = null,
        DateOnly? policyEffectiveFrom = null,
        decimal? policySumInsured = null) =>
        new(
            caseId ?? Guid.NewGuid(),
            name,
            email,
            phone,
            policy,
            claimType,
            completeness,
            createdAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            policyEffectiveFrom,
            policySumInsured);

    [Fact]
    public void Evaluate_NoClaims_ReturnsNoSignals()
    {
        Assert.Empty(FraudRuleEngine.Evaluate([]));
    }

    [Fact]
    public void Evaluate_CleanDistinctClaims_ReturnsNoSignals()
    {
        var claims = new[]
        {
            Claim("Aisha Nakato", phone: "+256700000001", policy: "POL-A"),
            Claim("David Mensah", phone: "+256700000002", policy: "POL-B"),
        };

        Assert.Empty(FraudRuleEngine.Evaluate(claims));
    }

    [Fact]
    public void Evaluate_SharedPhoneAcrossClaims_RaisesDuplicateContactSignal()
    {
        var claims = new[]
        {
            Claim("Aisha Nakato", phone: "+256 700 000 111", policy: "POL-A"),
            Claim("Aisha N.", phone: "256-700-000-111", policy: "POL-B"),
        };

        var signal = Assert.Single(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-DUP-CONTACT");
        Assert.Equal(FraudSignalSeverity.PotentialAnomaly, signal.Severity);
        Assert.Equal(2, signal.RelatedCaseIds.Count);
        Assert.DoesNotContain("confirmed", signal.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ThreeClaimsWithinWindow_RaisesVelocitySignal()
    {
        var start = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var claims = new[]
        {
            Claim("Fatima Suleiman", phone: "+256711000000", createdAt: start),
            Claim("Fatima Suleiman", phone: "+256711000000", createdAt: start.AddDays(5)),
            Claim("Fatima Suleiman", phone: "+256711000000", createdAt: start.AddDays(10)),
        };

        var signal = Assert.Single(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-VELOCITY");
        Assert.Equal(FraudSignalSeverity.RequiresInvestigation, signal.Severity);
        Assert.Equal(3, signal.RelatedCaseIds.Count);
    }

    [Fact]
    public void Evaluate_ThreeClaimsSpreadBeyondWindow_DoesNotRaiseVelocitySignal()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var claims = new[]
        {
            Claim("Peter Kagwa", phone: "+256712000000", createdAt: start),
            Claim("Peter Kagwa", phone: "+256712000000", createdAt: start.AddDays(40)),
            Claim("Peter Kagwa", phone: "+256712000000", createdAt: start.AddDays(90)),
        };

        Assert.DoesNotContain(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-VELOCITY");
    }

    [Fact]
    public void Evaluate_SamePolicyDifferentClaimants_RaisesDuplicatePolicySignal()
    {
        var claims = new[]
        {
            Claim("Grace Wanjiku", phone: "+256713000001", policy: "MTR-555"),
            Claim("Samuel Otieno", phone: "+256713000002", policy: "MTR-555"),
        };

        var signal = Assert.Single(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-DUP-POLICY");
        Assert.Equal(FraudSignalSeverity.PotentialAnomaly, signal.Severity);
    }

    [Fact]
    public void Evaluate_LowCompleteness_RaisesAdvisorySignalOnly()
    {
        var claims = new[] { Claim("Lydia Namutebi", phone: "+256714000000", completeness: 0.2m) };

        var signal = Assert.Single(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-LOW-COMPLETENESS");
        Assert.Equal(FraudSignalSeverity.AdvisorySignal, signal.Severity);
    }

    [Fact]
    public void Evaluate_NeverEmitsAccusatoryLanguage()
    {
        var start = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var claims = new[]
        {
            Claim("Repeat Claimant", phone: "+256715000000", policy: "MTR-9", completeness: 0.1m, createdAt: start),
            Claim("Repeat Claimant", phone: "+256715000000", policy: "MTR-9", completeness: 0.1m, createdAt: start.AddDays(3)),
            Claim("Other Person", phone: "+256715000000", policy: "MTR-9", completeness: 0.1m, createdAt: start.AddDays(6)),
        };

        foreach (var signal in FraudRuleEngine.Evaluate(claims))
        {
            foreach (var banned in new[] { "confirmed fraud", "fraudster", "guilty", "is fraud" })
            {
                Assert.DoesNotContain(banned, signal.Title, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(banned, signal.Description, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // WO 7.3: AML/CFT advisory signals, same "advisory only, never a determination" discipline
    // as the fraud rules above, evaluated from real policy data (EffectiveFrom/SumInsured), not
    // fabricated fields.

    [Fact]
    public void Evaluate_ClaimFiledWithinEarlyClaimWindow_RaisesAmlEarlyClaimSignal()
    {
        var claims = new[]
        {
            Claim("Grace Auma", phone: "+256700000200",
                createdAt: new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
                policyEffectiveFrom: new DateOnly(2026, 1, 1)),
        };

        var signal = Assert.Single(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-AML-EARLY-CLAIM");
        Assert.Equal(FraudSignalSeverity.PotentialAnomaly, signal.Severity);
        Assert.Contains("AML/CFT", signal.SignalType);
    }

    [Fact]
    public void Evaluate_ClaimFiledWellAfterPolicyInception_DoesNotRaiseEarlyClaimSignal()
    {
        var claims = new[]
        {
            Claim("Grace Auma", phone: "+256700000201",
                createdAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                policyEffectiveFrom: new DateOnly(2026, 1, 1)),
        };

        Assert.DoesNotContain(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-AML-EARLY-CLAIM");
    }

    [Fact]
    public void Evaluate_NoPolicyEffectiveDateKnown_DoesNotRaiseEarlyClaimSignal()
    {
        var claims = new[] { Claim("Grace Auma", phone: "+256700000202") }; // policyEffectiveFrom defaults to null

        Assert.DoesNotContain(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-AML-EARLY-CLAIM");
    }

    [Fact]
    public void Evaluate_SumInsuredMeetsThreshold_RaisesAmlHighValueSignal()
    {
        var claims = new[] { Claim("Wealthy Client", phone: "+256700000300", policySumInsured: 150_000_000m) };

        var signal = Assert.Single(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-AML-HIGH-VALUE");
        Assert.Equal(FraudSignalSeverity.AdvisorySignal, signal.Severity);
        Assert.Contains("AML/CFT", signal.SignalType);
    }

    [Fact]
    public void Evaluate_SumInsuredBelowThreshold_DoesNotRaiseHighValueSignal()
    {
        var claims = new[] { Claim("Modest Client", phone: "+256700000301", policySumInsured: 5_000_000m) };

        Assert.DoesNotContain(FraudRuleEngine.Evaluate(claims), s => s.RuleId == "R-AML-HIGH-VALUE");
    }

    [Fact]
    public void Evaluate_AmlCftSignals_NeverEmitAccusatoryLanguage()
    {
        var claims = new[]
        {
            Claim("Grace Auma", phone: "+256700000400",
                createdAt: new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
                policyEffectiveFrom: new DateOnly(2026, 1, 1), policySumInsured: 200_000_000m),
        };

        var signals = FraudRuleEngine.Evaluate(claims).Where(s => s.RuleId.StartsWith("R-AML-", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(signals);
        foreach (var signal in signals)
        {
            foreach (var banned in new[] { "confirmed", "guilty", "laundering has occurred", "is money laundering" })
            {
                Assert.DoesNotContain(banned, signal.Title, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(banned, signal.Description, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
