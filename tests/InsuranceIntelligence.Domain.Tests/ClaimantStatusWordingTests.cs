using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 19: a customer reads their own words for a claim's status, and a timeline
/// drawn from the product's stages.</summary>
public sealed class ClaimantStatusWordingTests
{
    public static IEnumerable<object[]> AllStatuses() => Enum.GetValues<CaseStatus>().Select(status => new object[] { status });

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusHasDefaultWordingForACustomer(CaseStatus status)
    {
        var wording = ClaimantStatusWords.DefaultFor(status);

        Assert.False(string.IsNullOrWhiteSpace(wording.Headline));
        Assert.False(string.IsNullOrWhiteSpace(wording.Detail));
        Assert.InRange(wording.Headline.Length, 1, ClaimantStatusWords.MaxHeadlineLength);
        Assert.InRange(wording.Detail.Length, 1, ClaimantStatusWords.MaxDetailLength);
        Assert.Contains(status, ClaimantStatusWords.Defaults.Keys);
    }

    [Fact]
    public void TheDefaultsUseNoStaffTermsNoDashesAndNoEmoji()
    {
        foreach (var wording in ClaimantStatusWords.Defaults.Values)
        {
            foreach (var text in new[] { wording.Headline, wording.Detail })
            {
                Assert.DoesNotContain((char)0x2014, text);
                Assert.DoesNotContain("--", text);
                Assert.False(text.Any(character => char.IsSurrogate(character)), "no emoji");
                Assert.DoesNotMatch("(?i)escalat|export|pending approval|supervisor|fraud|risk", text);
            }
        }
    }

    [Fact]
    public void AWorkspaceCanReplaceTheHeadlineTheDetailOrBoth()
    {
        var overrides = new Dictionary<string, StatusWording>
        {
            [nameof(CaseStatus.New)] = new("Thanks, we have it", ""),
            [nameof(CaseStatus.Closed)] = new("", "Closed on our side."),
            [nameof(CaseStatus.Settled)] = new("Paid out", "Your money is on its way."),
        };

        Assert.Equal(new StatusWording("Thanks, we have it", ClaimantStatusWords.DefaultFor(CaseStatus.New).Detail), ClaimantStatusWords.For(CaseStatus.New, overrides));
        Assert.Equal(new StatusWording(ClaimantStatusWords.DefaultFor(CaseStatus.Closed).Headline, "Closed on our side."), ClaimantStatusWords.For(CaseStatus.Closed, overrides));
        Assert.Equal(new StatusWording("Paid out", "Your money is on its way."), ClaimantStatusWords.For(CaseStatus.Settled, overrides));
        Assert.Equal(ClaimantStatusWords.DefaultFor(CaseStatus.InReview), ClaimantStatusWords.For(CaseStatus.InReview, overrides));
        Assert.Equal(ClaimantStatusWords.DefaultFor(CaseStatus.InReview), ClaimantStatusWords.For(CaseStatus.InReview, null));
    }

    [Fact]
    public void NormaliseDropsUnknownStatusesAndEntriesThatEqualTheDefaultAndScrubsDashes()
    {
        var input = new Dictionary<string, StatusWording>
        {
            ["Nonsense"] = new("x", "y"),
            [nameof(CaseStatus.New)] = ClaimantStatusWords.DefaultFor(CaseStatus.New),
            [nameof(CaseStatus.Assigned)] = new("  ", "  "),
            ["inreview"] = new("We are on it " + (char)0x2014 + " promise", "Soon."),
        };

        var result = ClaimantStatusWords.Normalise(input)!;

        var entry = Assert.Single(result);
        Assert.Equal(nameof(CaseStatus.InReview), entry.Key);
        Assert.DoesNotContain((char)0x2014, entry.Value.Headline);
        Assert.Equal("Soon.", entry.Value.Detail);
        Assert.Null(ClaimantStatusWords.Normalise(new Dictionary<string, StatusWording>()));
        Assert.Null(ClaimantStatusWords.Normalise(null));
    }

    [Fact]
    public void LengthsAreChecked()
    {
        var tooLong = new Dictionary<string, StatusWording>
        {
            [nameof(CaseStatus.New)] = new(new string('a', ClaimantStatusWords.MaxHeadlineLength + 1), new string('b', ClaimantStatusWords.MaxDetailLength + 1)),
        };

        Assert.Equal(2, ClaimantStatusWords.Validate(tooLong).Count);
        Assert.Empty(ClaimantStatusWords.Validate(null));
    }

    private static string[] Labels(IEnumerable<ClaimantTimelineStep> steps) => steps.Select(step => step.Label).ToArray();

    private static string StateLine(IEnumerable<ClaimantTimelineStep> steps) =>
        string.Join(",", steps.Select(step => step.State switch { TimelineState.Done => "D", TimelineState.Current => "C", _ => "U" }));

    [Theory]
    [InlineData(CaseStatus.New, "C,U,U,U,U,U")]
    [InlineData(CaseStatus.Assigned, "D,C,U,U,U,U")]
    [InlineData(CaseStatus.MissingDocuments, "D,C,U,U,U,U")]
    [InlineData(CaseStatus.InReview, "D,D,C,U,U,U")]
    [InlineData(CaseStatus.Escalated, "D,D,D,C,U,U")]
    [InlineData(CaseStatus.ReadyForReview, "D,D,D,C,U,U")]
    [InlineData(CaseStatus.PendingApproval, "D,D,D,D,C,U")]
    [InlineData(CaseStatus.ReadyForExport, "D,D,D,D,D,C")]
    [InlineData(CaseStatus.Settled, "D,D,D,D,D,D")]
    [InlineData(CaseStatus.Closed, "D,D,D,D,D,D")]
    public void TheDefaultProcessGivesSixCustomerSteps(CaseStatus status, string expected)
    {
        var steps = ClaimantTimeline.Build(status, DefaultClaimStageLibrary.Default());

        Assert.Equal(["Claim received", "Documents and details", "Assessment", "Report prepared", "Decision", "Settlement"], Labels(steps));
        Assert.Equal(expected, StateLine(steps));
    }

    [Fact]
    public void StagesForStaffOnlyAreNotShownToTheCustomer()
    {
        var steps = ClaimantTimeline.Build(CaseStatus.New, DefaultClaimStageLibrary.Default());

        Assert.DoesNotContain(steps, step => step.Key == DefaultClaimStageLibrary.DischargeVoucher || step.Key == DefaultClaimStageLibrary.ComplaintEscalation);
    }

    [Fact]
    public void AProductCanRenameHideAndAddStagesForTheCustomer()
    {
        var stages = new List<ClaimStageDefinition>(DefaultClaimStageLibrary.Default());
        stages[0] = stages[0] with { CustomerLabel = "We have your claim" };
        stages[3] = stages[3] with { CustomerVisible = false };
        stages.Add(new ClaimStageDefinition("garage_inspection", "Garage inspection", "Assessor", false, 9, null, CustomerLabel: "Garage inspection", CustomerVisible: true, MilestoneStatus: CaseStatus.ReadyForExport));
        stages.Add(new ClaimStageDefinition("internal_only", "Internal", "Finance", false, 10, null));

        var steps = ClaimantTimeline.Build(CaseStatus.InReview, stages);

        Assert.Equal("We have your claim", steps[0].Label);
        Assert.DoesNotContain(steps, step => step.Key == DefaultClaimStageLibrary.AdjustmentOrInvestigation);
        Assert.Contains(steps, step => step.Key == "garage_inspection");
        Assert.DoesNotContain(steps, step => step.Key == "internal_only");
        Assert.Equal(1, steps.Count(step => step.State == TimelineState.Current));
    }

    [Fact]
    public void ACustomStageNeedsAMilestoneToAppear()
    {
        var stages = new List<ClaimStageDefinition>
        {
            new("a", "Step A", "Officer", false, 1, null, CustomerVisible: true, MilestoneStatus: CaseStatus.New),
            new("b", "Step B", "Officer", false, 2, null, CustomerVisible: true),
        };

        Assert.Equal(["Step A"], Labels(ClaimantTimeline.Build(CaseStatus.New, stages)));
    }

    [Theory]
    [InlineData(CaseStatus.New, "C,U,U,U,U")]
    [InlineData(CaseStatus.InReview, "D,D,C,U,U")]
    [InlineData(CaseStatus.Settled, "D,D,D,D,D")]
    public void AProductWithNoStagesGetsAPlainFiveStepTimeline(CaseStatus status, string expected)
    {
        foreach (var stages in new IReadOnlyList<ClaimStageDefinition>?[] { null, [] })
        {
            var steps = ClaimantTimeline.Build(status, stages);
            Assert.Equal(["Claim received", "Under review", "Assessment", "Decision", "Settlement"], Labels(steps));
            Assert.Equal(expected, StateLine(steps));
        }
    }

    [Fact]
    public void TheCurrentStepIsNeverMoreThanOne()
    {
        foreach (var status in Enum.GetValues<CaseStatus>())
        {
            Assert.InRange(ClaimantTimeline.Build(status, DefaultClaimStageLibrary.Default()).Count(step => step.State == TimelineState.Current), 0, 1);
        }
    }
}
