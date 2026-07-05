using InsuranceIntelligence.Domain.Underwriting;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class UnderwritingRuleEngineTests
{
    private static UnderwritingSubmissionInput Submission(
        decimal sumInsured = 50_000_000m,
        string product = "Motor Comprehensive",
        string[]? missing = null) =>
        new("SUB-1", "Applicant Ltd", "Direct", product, sumInsured, "UGX", missing ?? []);

    [Fact]
    public void Review_CompleteStandardSubmission_AllPass_NoQuestions()
    {
        var review = UnderwritingRuleEngine.Review(Submission());

        Assert.All(review.Checks, c => Assert.Equal(UnderwritingCheckState.Pass, c.State));
        Assert.Empty(review.DraftedQuestions);
    }

    [Fact]
    public void Review_MissingItems_FlagsMissingAndDraftsAQuestionPerItem()
    {
        var review = UnderwritingRuleEngine.Review(Submission(missing: ["driving permit", "vehicle photos"]));

        var completeness = Assert.Single(review.Checks, c => c.CheckId == "UW-COMPLETENESS");
        Assert.Equal(UnderwritingCheckState.MissingInformation, completeness.State);
        Assert.Equal(2, review.DraftedQuestions.Count);
    }

    [Fact]
    public void Review_SumInsuredAboveThreshold_RefersToSeniorUnderwriter()
    {
        var review = UnderwritingRuleEngine.Review(Submission(sumInsured: 420_000_000m));

        var limit = Assert.Single(review.Checks, c => c.CheckId == "UW-LIMIT");
        Assert.Equal(UnderwritingCheckState.Attention, limit.State);
        Assert.Contains("senior", limit.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Review_HigherRiskProductLine_RaisesProductCheck()
    {
        var review = UnderwritingRuleEngine.Review(Submission(product: "Marine Cargo"));

        Assert.Contains(review.Checks, c => c.CheckId == "UW-PRODUCT" && c.State == UnderwritingCheckState.Attention);
    }

    [Fact]
    public void Review_NeverDecidesAcceptanceRejectionOrPremium()
    {
        var review = UnderwritingRuleEngine.Review(Submission(sumInsured: 999_000_000m, product: "Marine Cargo", missing: ["survey"]));

        foreach (var text in review.Checks.Select(c => c.Detail).Concat(review.DraftedQuestions))
        {
            foreach (var banned in new[] { "accepted", "rejected", "declined", "approved", "premium is", "bind" })
            {
                Assert.DoesNotContain(banned, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
