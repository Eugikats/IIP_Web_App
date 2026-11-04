using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 18: the review score and the recommendation are plain rules over reasons
/// that a person can read, and a model on its own can never raise them far.</summary>
public sealed class ClaimAssessmentTests
{
    private static readonly Guid CaseId = Guid.NewGuid();

    private static RiskFactor Factor(string code, RiskFactorSource source, decimal weight, HumanReading reading = HumanReading.NotLookedAt) =>
        new(code, source, weight, $"Reason for {code}.", new AssessmentSource(SourceKind.Signal, code, code), reading);

    private static RecommendationFacts Facts(RiskScore score, MissingDocumentRequirement[]? missing = null, int pending = 0, (Guid, string)[]? unreadable = null) =>
        new(score, missing ?? [], unreadable ?? [], pending, CaseId);

    [Fact]
    public void NoReasonsMeansAScoreOfZeroAndLow()
    {
        var score = ClaimRiskScorer.Score([]);
        Assert.Equal(0, score.Value);
        Assert.Equal(RiskBand.Low, score.Band);
    }

    [Fact]
    public void EachReasonAddsLessThanTheLastAndTheTotalStaysBelowOneHundred()
    {
        var one = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.RuleEngine, 0.45m)]);
        var two = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.RuleEngine, 0.45m), Factor("b", RiskFactorSource.RuleEngine, 0.45m)]);
        var many = ClaimRiskScorer.Score(Enumerable.Range(0, 30).Select(i => Factor($"f{i}", RiskFactorSource.RuleEngine, 0.45m)).ToArray());

        Assert.Equal(45, one.Value);
        Assert.Equal(70, two.Value);
        Assert.True(two.Value - one.Value < one.Value);
        Assert.InRange(many.Value, 99, 100);
    }

    [Theory]
    [InlineData(0, RiskBand.Low)]
    [InlineData(19, RiskBand.Low)]
    [InlineData(20, RiskBand.Moderate)]
    [InlineData(44, RiskBand.Moderate)]
    [InlineData(45, RiskBand.Elevated)]
    [InlineData(69, RiskBand.Elevated)]
    [InlineData(70, RiskBand.High)]
    [InlineData(100, RiskBand.High)]
    public void BandsFollowTheScore(int value, RiskBand band) => Assert.Equal(band, ClaimRiskScorer.BandFor(value));

    [Fact]
    public void AReasonAPersonSetAsideNoLongerCounts()
    {
        var open = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.PictureCheck, 0.40m)]);
        var setAside = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.PictureCheck, 0.40m, HumanReading.SetAside)]);

        Assert.Equal(40, open.Value);
        Assert.Equal(0, setAside.Value);
        Assert.False(Assert.Single(setAside.Factors).Counts);
    }

    [Fact]
    public void AModelOnItsOwnCanNeverRaiseTheBandAboveModerateNoMatterHowMuchItWrites()
    {
        var notes = Enumerable.Range(0, 50).Select(i => Factor($"m{i}", RiskFactorSource.ModelReading, ClaimRiskScorer.ModelFactorWeight)).ToArray();
        var score = ClaimRiskScorer.Score(notes);

        Assert.True(score.Value < 20, $"score was {score.Value}");
        Assert.Equal(RiskBand.Low, score.Band);
        Assert.Equal(ClaimRiskScorer.MaxModelFactors, score.Counted.Count);
        Assert.Equal(ClaimRiskScorer.MaxModelFactors, score.Factors.Count);
    }

    [Fact]
    public void TheSameReasonsInAnyOrderGiveTheSameScoreAndHash()
    {
        var a = Factor("a", RiskFactorSource.RuleEngine, 0.25m);
        var b = Factor("b", RiskFactorSource.PictureCheck, 0.20m);
        var first = ClaimRiskScorer.Score([a, b]);
        var second = ClaimRiskScorer.Score([b, a]);

        Assert.Equal(first.Value, second.Value);
        Assert.Equal(
            ClaimAssessment.HashInputs(first, ClaimRecommender.Recommend(Facts(first))),
            ClaimAssessment.HashInputs(second, ClaimRecommender.Recommend(Facts(second))));
    }

    [Fact]
    public void ADifferentReadingByAPersonChangesTheHash()
    {
        var open = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.PictureCheck, 0.40m)]);
        var done = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.PictureCheck, 0.40m, HumanReading.SetAside)]);

        Assert.NotEqual(
            ClaimAssessment.HashInputs(open, ClaimRecommender.Recommend(Facts(open))),
            ClaimAssessment.HashInputs(done, ClaimRecommender.Recommend(Facts(done))));
    }

    [Fact]
    public void NothingOutstandingMeansReadyForADecisionWithAReasonThatSaysSo()
    {
        var recommendation = ClaimRecommender.Recommend(Facts(ClaimRiskScorer.Score([])));

        Assert.Equal(RecommendationKind.ReadyForDecision, recommendation.Kind);
        Assert.NotEmpty(recommendation.Reasons);
        Assert.Empty(recommendation.SuggestedRequests);
    }

    [Fact]
    public void MissingDocumentsMeanAskForThemAndEachRequestNamesTheDocument()
    {
        var recommendation = ClaimRecommender.Recommend(Facts(
            ClaimRiskScorer.Score([]),
            missing: [new MissingDocumentRequirement("PoliceAbstract", "Police abstract", true)]));

        Assert.Equal(RecommendationKind.RequestInformation, recommendation.Kind);
        Assert.Contains("Ask the customer for: Police abstract", recommendation.SuggestedRequests);
        Assert.Contains(recommendation.Reasons, reason => reason.Source.Kind == SourceKind.Checklist && reason.Source.Label == "Police abstract");
    }

    [Fact]
    public void AnUnreadableDocumentMeansAskForItAgainAndPointsAtTheDocument()
    {
        var documentId = Guid.NewGuid();
        var recommendation = ClaimRecommender.Recommend(Facts(ClaimRiskScorer.Score([]), unreadable: [(documentId, "Photo 3")]));

        Assert.Equal(RecommendationKind.RequestInformation, recommendation.Kind);
        Assert.Contains(recommendation.Reasons, reason => reason.Source.Kind == SourceKind.Document && reason.Source.Id == documentId.ToString());
    }

    [Fact]
    public void AFlagNobodyHasLookedAtMeansLookAtItBeforeAskingForAnythingElse()
    {
        var recommendation = ClaimRecommender.Recommend(Facts(
            ClaimRiskScorer.Score([Factor("pic", RiskFactorSource.PictureCheck, 0.20m)]),
            missing: [new MissingDocumentRequirement("PoliceAbstract", "Police abstract", true)]));

        Assert.Equal(RecommendationKind.ReviewFlaggedItems, recommendation.Kind);
        Assert.Contains(recommendation.Reasons, reason => reason.Source.Label == "pic");
        Assert.Contains(recommendation.Reasons, reason => reason.Source.Kind == SourceKind.Checklist);
    }

    [Fact]
    public void AHighScoreMeansConsiderReferringAndStatesTheScoreFirst()
    {
        var score = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.RuleEngine, 0.45m), Factor("b", RiskFactorSource.PictureCheck, 0.45m)]);
        var recommendation = ClaimRecommender.Recommend(Facts(score));

        Assert.Equal(RiskBand.High, score.Band);
        Assert.Equal(RecommendationKind.ReferForInvestigation, recommendation.Kind);
        Assert.Equal(SourceKind.Score, recommendation.Reasons[0].Source.Kind);
    }

    [Fact]
    public void AConcernAPersonRaisedMeansConsiderReferringEvenWhenTheScoreIsLow()
    {
        var score = ClaimRiskScorer.Score([Factor("a", RiskFactorSource.PictureCheck, 0.05m, HumanReading.ConcernRaised)]);

        Assert.Equal(RiskBand.Low, score.Band);
        Assert.Equal(RecommendationKind.ReferForInvestigation, ClaimRecommender.Recommend(Facts(score)).Kind);
    }

    [Fact]
    public void WorkInProgressOnAFlagMeansWaitForIt()
    {
        var recommendation = ClaimRecommender.Recommend(Facts(
            ClaimRiskScorer.Score([Factor("a", RiskFactorSource.RuleEngine, 0.25m, HumanReading.BeingWorked)])));

        Assert.Equal(RecommendationKind.ReviewFlaggedItems, recommendation.Kind);
        Assert.Contains("still being worked", recommendation.Headline);
    }

    [Fact]
    public void ValuesWaitingForAPersonMeanCheckThemFirst()
    {
        var recommendation = ClaimRecommender.Recommend(Facts(ClaimRiskScorer.Score([]), pending: 2));

        Assert.Equal(RecommendationKind.RequestInformation, recommendation.Kind);
        Assert.Contains(recommendation.Reasons, reason => reason.Source.Kind == SourceKind.Field && reason.Text.StartsWith("2 values"));
    }

    [Fact]
    public void SeveritiesMapToWeightsInOrder()
    {
        Assert.True(ClaimRiskScorer.WeightFor(FraudSignalSeverity.AdvisorySignal) < ClaimRiskScorer.WeightFor(FraudSignalSeverity.PotentialAnomaly));
        Assert.True(ClaimRiskScorer.WeightFor(FraudSignalSeverity.PotentialAnomaly) < ClaimRiskScorer.WeightFor(FraudSignalSeverity.RequiresInvestigation));
        Assert.Equal(0m, ClaimRiskScorer.WeightFor(AuthenticitySeverity.Info));
        Assert.True(ClaimRiskScorer.WeightFor(AuthenticitySeverity.Medium) < ClaimRiskScorer.WeightFor(AuthenticitySeverity.High));
    }

    [Theory]
    [InlineData(AuthenticityDisposition.NotReviewed, HumanReading.NotLookedAt)]
    [InlineData(AuthenticityDisposition.NoConcern, HumanReading.SetAside)]
    [InlineData(AuthenticityDisposition.NeedsFollowUp, HumanReading.FollowUpRequested)]
    [InlineData(AuthenticityDisposition.ConcernRaised, HumanReading.ConcernRaised)]
    public void APersonsPictureReadingMapsToWhatItDoesToTheScore(AuthenticityDisposition disposition, HumanReading reading) =>
        Assert.Equal(reading, ClaimRiskScorer.ReadingFor(disposition));

    [Fact]
    public void AnUntriagedSignalIsNotLookedAtAndAClosedNoActionOneIsSetAside()
    {
        Assert.Equal(HumanReading.NotLookedAt, ClaimRiskScorer.ReadingFor((FraudSignalDisposition?)null));

        FraudSignalDisposition Disposition(FraudDispositionStatus status, FraudDispositionOutcome? outcome) => new()
        {
            SignalId = Guid.NewGuid(),
            Scope = new InsuranceIntelligence.Domain.Common.TenantScope("t", "w"),
            RuleId = "R",
            RelatedCaseIds = [CaseId],
            Status = status,
            Outcome = outcome,
            CreatedByUserId = "u",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "c",
        };

        Assert.Equal(HumanReading.BeingWorked, ClaimRiskScorer.ReadingFor(Disposition(FraudDispositionStatus.Triaged, null)));
        Assert.Equal(HumanReading.SetAside, ClaimRiskScorer.ReadingFor(Disposition(FraudDispositionStatus.OutcomeRecorded, FraudDispositionOutcome.NoFurtherActionRequired)));
        Assert.Equal(HumanReading.SetAside, ClaimRiskScorer.ReadingFor(Disposition(FraudDispositionStatus.Closed, FraudDispositionOutcome.UnableToSubstantiateConcern)));
        Assert.Equal(HumanReading.ConcernRaised, ClaimRiskScorer.ReadingFor(Disposition(FraudDispositionStatus.OutcomeRecorded, FraudDispositionOutcome.ReferredForSpecialInvestigation)));
    }

    [Theory]
    [InlineData("Two sentences that explain the next step. A person decides.", true)]
    [InlineData("The claim is approved.", false)]
    [InlineData("This was rejected by the team.", false)]
    [InlineData("The customer committed fraud.", false)]
    [InlineData("This is fraudulent.", false)]
    [InlineData("We should pay the claim now.", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void AdvisoryWordingKeepsDecisionAndAccusationWordsOut(string text, bool acceptable) =>
        Assert.Equal(acceptable, AdvisoryLanguage.IsAcceptable(text));
}
