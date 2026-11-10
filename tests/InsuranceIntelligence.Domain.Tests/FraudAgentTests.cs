using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>The fraud agent's playbook and what is kept of its output. The agent can raise a concern only
/// by citing facts it was really shown, and never in the words of a decision or an accusation.</summary>
public sealed class FraudAgentTests
{
    private static readonly IReadOnlySet<string> Facts = new HashSet<string> { "F1", "F2", "F3", "F4" };

    private static string Finding(
        string indicator = "MOT-EARLY-LOSS",
        string title = "Loss shortly after cover began",
        string observation = "The incident was 3 days after the cover began.",
        string check = "Compare the premium receipt time with the incident time.",
        string severity = "potential",
        string facts = "[\"F2\"]") =>
        $"{{\"indicator\":\"{indicator}\",\"title\":\"{title}\",\"observation\":\"{observation}\",\"check\":\"{check}\",\"severity\":\"{severity}\",\"facts\":{facts}}}";

    private static string Reply(params string[] findings) => $"{{\"findings\":[{string.Join(',', findings)}]}}";

    [Fact]
    public void ThePlaybookCoversTheMainLinesAndEveryIndicatorSaysWhatToCheckAndWhatIsOrdinary()
    {
        var lines = FraudPlaybook.Indicators.Select(indicator => indicator.Line).Distinct().ToArray();
        foreach (var line in new[] { "Motor", "Medical", "Life", "Property", "Agriculture", "Any" })
        {
            Assert.Contains(line, lines);
        }

        Assert.Equal(FraudPlaybook.Indicators.Count, FraudPlaybook.Indicators.Select(indicator => indicator.Code).Distinct().Count());
        foreach (var indicator in FraudPlaybook.Indicators)
        {
            Assert.False(string.IsNullOrWhiteSpace(indicator.WhatItLooksLike), indicator.Code);
            Assert.False(string.IsNullOrWhiteSpace(indicator.OrdinaryExplanations), indicator.Code);
            Assert.False(string.IsNullOrWhiteSpace(indicator.WhatToCheck), indicator.Code);
        }
    }

    [Fact]
    public void ThePlaybookNeverUsesAnEmDashOrAccusationWording()
    {
        var text = FraudPlaybook.RenderForPrompt() + FraudPlaybook.LocalContext;
        Assert.DoesNotContain('—', text);
        Assert.True(AdvisoryLanguage.IsAcceptable(string.Join(' ', FraudPlaybook.Indicators.Select(indicator => indicator.WhatItLooksLike))));
    }

    [Fact]
    public void TheRenderedPlaybookListsEveryCodeTheAgentMayUse()
    {
        var rendered = FraudPlaybook.RenderForPrompt();
        foreach (var indicator in FraudPlaybook.Indicators)
        {
            Assert.Contains($"[{indicator.Code}]", rendered);
        }

        Assert.Contains($"[{FraudPlaybook.OtherCode}]", rendered);
    }

    [Fact]
    public void AFindingThatCitesARealFactIsKept()
    {
        var parsed = FraudAgentOutput.Parse(Reply(Finding()), Facts);

        Assert.True(parsed.Readable);
        var finding = Assert.Single(parsed.Findings);
        Assert.Equal("MOT-EARLY-LOSS", finding.IndicatorCode);
        Assert.Equal(new[] { "F2" }, finding.FactIds);
        Assert.Equal(FraudSignalSeverity.PotentialAnomaly, finding.Severity);
        Assert.Equal(0, parsed.LeftOut);
    }

    [Fact]
    public void AFindingThatCitesNoFactOrAnInventedOneIsDropped()
    {
        var parsed = FraudAgentOutput.Parse(Reply(Finding(facts: "[]"), Finding(facts: "[\"F99\"]"), Finding(facts: "[\"F2\"]")), Facts);

        Assert.Single(parsed.Findings);
        Assert.Equal(2, parsed.LeftOut);
    }

    [Theory]
    [InlineData("This claim is fraudulent.")]
    [InlineData("The claimant is lying about the date.")]
    [InlineData("The claim should be denied.")]
    [InlineData("This is confirmed fraud.")]
    public void AccusationOrDecisionWordingIsDropped(string observation)
    {
        var parsed = FraudAgentOutput.Parse(Reply(Finding(observation: observation)), Facts);

        Assert.Empty(parsed.Findings);
        Assert.Equal(1, parsed.LeftOut);
    }

    [Fact]
    public void ACodeOutsideThePlaybookIsDroppedButOtherIsAllowed()
    {
        var parsed = FraudAgentOutput.Parse(Reply(Finding(indicator: "MADE-UP"), Finding(indicator: "OTHER")), Facts);

        var finding = Assert.Single(parsed.Findings);
        Assert.Equal("OTHER", finding.IndicatorCode);
        Assert.Equal(1, parsed.LeftOut);
    }

    [Fact]
    public void OneFactCanRaiseAtMostAPotentialAnomalyAndTwoFactsCanRaiseAnInvestigation()
    {
        var one = FraudAgentOutput.Parse(Reply(Finding(severity: "investigate", facts: "[\"F2\"]")), Facts);
        var two = FraudAgentOutput.Parse(Reply(Finding(severity: "investigate", facts: "[\"F2\",\"F3\"]")), Facts);

        Assert.Equal(FraudSignalSeverity.PotentialAnomaly, one.Findings[0].Severity);
        Assert.Equal(FraudSignalSeverity.RequiresInvestigation, two.Findings[0].Severity);
    }

    [Fact]
    public void ReplyWrappedInACodeFenceIsStillRead()
    {
        var parsed = FraudAgentOutput.Parse("Here is the review:\n```json\n" + Reply(Finding()) + "\n```", Facts);

        Assert.True(parsed.Readable);
        Assert.Single(parsed.Findings);
    }

    [Fact]
    public void AnEmptyListIsAValidAnswer()
    {
        var parsed = FraudAgentOutput.Parse("{\"findings\":[]}", Facts);

        Assert.True(parsed.Readable);
        Assert.Empty(parsed.Findings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("I could not find anything.")]
    [InlineData("{not json")]
    public void AReplyThatCannotBeReadIsReportedAsUnreadableNotAsNoConcerns(string content)
    {
        var parsed = FraudAgentOutput.Parse(content, Facts);

        Assert.False(parsed.Readable);
        Assert.Empty(parsed.Findings);
    }

    [Fact]
    public void EmDashesInTheModelsWordsAreRemoved()
    {
        var parsed = FraudAgentOutput.Parse(Reply(Finding(observation: "Loss soon after cover began — worth a check.")), Facts);

        Assert.DoesNotContain('—', parsed.Findings[0].Observation);
    }

    [Fact]
    public void NoMoreThanSixFindingsAreKept()
    {
        var many = Enumerable.Range(0, 10).Select(i => Finding(indicator: FraudPlaybook.Indicators[i].Code)).ToArray();
        var parsed = FraudAgentOutput.Parse(Reply(many), Facts);

        Assert.Equal(FraudAgentOutput.MaxFindings, parsed.Findings.Count);
        Assert.Equal(4, parsed.LeftOut);
    }

    [Fact]
    public void FraudAgentFindingsCountForLessThanFixedRulesAndAreCappedAtThree()
    {
        Assert.True(ClaimRiskScorer.AgentWeightFor(FraudSignalSeverity.PotentialAnomaly) < ClaimRiskScorer.WeightFor(FraudSignalSeverity.PotentialAnomaly));
        Assert.True(ClaimRiskScorer.AgentWeightFor(FraudSignalSeverity.RequiresInvestigation) < ClaimRiskScorer.WeightFor(FraudSignalSeverity.RequiresInvestigation));

        RiskFactor AgentFactor(int i) => new($"agent:{i}", RiskFactorSource.FraudAgent, ClaimRiskScorer.AgentWeightFor(FraudSignalSeverity.RequiresInvestigation),
            $"Reason {i}.", new AssessmentSource(SourceKind.ModelReading, "r", "Review"), HumanReading.NotLookedAt);
        var score = ClaimRiskScorer.Score(Enumerable.Range(0, 8).Select(AgentFactor).ToArray());

        Assert.Equal(ClaimRiskScorer.MaxFraudAgentFactors, score.Factors.Count);
        Assert.True(score.Value < 70, "a model's reading on its own should not reach the High band");
    }

    [Fact]
    public void TheFactsHashChangesWhenAFactChanges()
    {
        var a = FraudAgentReview.HashFacts([new FraudAgentFact("F1", "one"), new FraudAgentFact("F2", "two")]);
        var same = FraudAgentReview.HashFacts([new FraudAgentFact("F1", "one"), new FraudAgentFact("F2", "two")]);
        var changed = FraudAgentReview.HashFacts([new FraudAgentFact("F1", "one"), new FraudAgentFact("F2", "three")]);

        Assert.Equal(a, same);
        Assert.NotEqual(a, changed);
    }
}
