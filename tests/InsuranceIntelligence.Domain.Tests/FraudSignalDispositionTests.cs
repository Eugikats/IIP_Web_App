using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class FraudSignalDispositionTests
{
    [Fact]
    public void DeterministicId_IsStable_ForTheSameRuleAndRelatedCases()
    {
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();

        var first = FraudSignalDisposition.DeterministicId("R-VELOCITY", [caseA, caseB]);
        var second = FraudSignalDisposition.DeterministicId("R-VELOCITY", [caseA, caseB]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeterministicId_IsOrderIndependent_ForRelatedCases()
    {
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();

        var forward = FraudSignalDisposition.DeterministicId("R-DUP-CONTACT", [caseA, caseB]);
        var reversed = FraudSignalDisposition.DeterministicId("R-DUP-CONTACT", [caseB, caseA]);

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void DeterministicId_DiffersForDifferentRulesOrCases()
    {
        var caseA = Guid.NewGuid();
        var caseB = Guid.NewGuid();

        var byRule = FraudSignalDisposition.DeterministicId("R-VELOCITY", [caseA]);
        var byOtherRule = FraudSignalDisposition.DeterministicId("R-DUP-CONTACT", [caseA]);
        var byOtherCase = FraudSignalDisposition.DeterministicId("R-VELOCITY", [caseB]);

        Assert.NotEqual(byRule, byOtherRule);
        Assert.NotEqual(byRule, byOtherCase);
    }

    [Fact]
    public void DispositionOutcome_NeverContainsAnAdverseFraudDetermination()
    {
        // WO-19 language contract (I4, DEC-014): IIP never asserts confirmed fraud, even as a
        // human-recorded outcome. A structural guardrail — this fails the moment anyone adds an
        // enum value implying a determination, forcing that choice to be reconsidered here rather
        // than slipping in silently.
        var forbiddenFragments = new[] { "confirm", "guilty", "fraudulent", "proven" };
        foreach (var name in Enum.GetNames<FraudDispositionOutcome>())
        {
            var normalized = name.ToLowerInvariant();
            Assert.All(forbiddenFragments, fragment =>
                Assert.DoesNotContain(fragment, normalized, StringComparison.Ordinal));
        }
    }
}
