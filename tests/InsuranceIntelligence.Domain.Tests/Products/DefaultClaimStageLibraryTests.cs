using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests.Products;

public sealed class DefaultClaimStageLibraryTests
{
    [Fact]
    public void Default_HasTheEightStagesUgandanClaimsActuallyGoThrough()
    {
        var stages = DefaultClaimStageLibrary.Default();

        Assert.Equal(8, stages.Count);
        Assert.Equal(
            [
                DefaultClaimStageLibrary.Intimation,
                DefaultClaimStageLibrary.AcknowledgementAndDocumentRequest,
                DefaultClaimStageLibrary.AssessorOrSiteVisit,
                DefaultClaimStageLibrary.AdjustmentOrInvestigation,
                DefaultClaimStageLibrary.OfferOrRepudiation,
                DefaultClaimStageLibrary.DischargeVoucher,
                DefaultClaimStageLibrary.Payment,
                DefaultClaimStageLibrary.ComplaintEscalation,
            ],
            stages.OrderBy(stage => stage.SortOrder).Select(stage => stage.StageKey));
    }

    [Fact]
    public void Default_StageKeysAreUnique()
    {
        var stages = DefaultClaimStageLibrary.Default();

        Assert.Equal(stages.Count, stages.Select(stage => stage.StageKey).Distinct().Count());
    }

    [Fact]
    public void Default_AcknowledgementStage_MatchesIraThreeWorkingDayStandard()
    {
        var stage = DefaultClaimStageLibrary.Default()
            .Single(stage => stage.StageKey == DefaultClaimStageLibrary.AcknowledgementAndDocumentRequest);

        Assert.Equal(3, stage.ServiceStandardWorkingDays);
    }

    [Fact]
    public void Default_OfferOrRepudiationStage_MatchesIraFiveWorkingDayStandard()
    {
        var stage = DefaultClaimStageLibrary.Default()
            .Single(stage => stage.StageKey == DefaultClaimStageLibrary.OfferOrRepudiation);

        Assert.Equal(5, stage.ServiceStandardWorkingDays);
    }

    [Theory]
    [InlineData(5_000_000, 10)]
    [InlineData(10_000_000, 10)]
    [InlineData(25_000_000, 15)]
    [InlineData(50_000_000, 15)]
    [InlineData(75_000_000, 20)]
    public void Default_PaymentStage_ResolvesTheCorrectSettlementClockTierByClaimValue(decimal claimValue, int expectedWorkingDays)
    {
        var stage = DefaultClaimStageLibrary.Default()
            .Single(stage => stage.StageKey == DefaultClaimStageLibrary.Payment);

        Assert.Equal(expectedWorkingDays, stage.ResolveServiceStandardWorkingDays(claimValue));
    }

    [Fact]
    public void Default_IntimationStage_HasNoFixedDayCount()
    {
        var stage = DefaultClaimStageLibrary.Default()
            .Single(stage => stage.StageKey == DefaultClaimStageLibrary.Intimation);

        Assert.Null(stage.ServiceStandardWorkingDays);
        Assert.Null(stage.ResolveServiceStandardWorkingDays(claimValueUgx: 1_000_000m));
    }
}
