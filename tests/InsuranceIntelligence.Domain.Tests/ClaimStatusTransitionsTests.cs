using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 20: every status is reachable by a defined path, and the moves that decide
/// something keep their own actions.</summary>
public sealed class ClaimStatusTransitionsTests
{
    public static IEnumerable<object[]> AllStatuses() => Enum.GetValues<CaseStatus>().Select(status => new object[] { status });

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusIsReachableFromANewClaim(CaseStatus status) =>
        Assert.Contains(status, ClaimStatusTransitions.ReachableFromNew());

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusExplainsHowItIsReached(CaseStatus status) =>
        Assert.False(string.IsNullOrWhiteSpace(ClaimStatusTransitions.HowItIsReached(status)));

    [Theory]
    [InlineData(CaseStatus.PendingApproval)]
    [InlineData(CaseStatus.ReadyForExport)]
    [InlineData(CaseStatus.Settled)]
    [InlineData(CaseStatus.Closed)]
    [InlineData(CaseStatus.Escalated)]
    [InlineData(CaseStatus.Assigned)]
    [InlineData(CaseStatus.New)]
    public void StatusesThatDecideSomethingCannotBeChosenFromTheList(CaseStatus target)
    {
        Assert.DoesNotContain(ClaimStatusTransitions.All, transition => transition.Path == StatusChangePath.ManualChange && transition.To == target);
    }

    [Theory]
    [InlineData(CaseStatus.PendingApproval)]
    [InlineData(CaseStatus.ReadyForExport)]
    [InlineData(CaseStatus.Settled)]
    public void AClaimInTheseStatusesCannotBeMovedByHand(CaseStatus from) =>
        Assert.Empty(ClaimStatusTransitions.ManualFrom(from));

    [Fact]
    public void TakingAClaimOutOfEscalationOrReopeningAClosedOneNeedsASupervisor()
    {
        Assert.True(ClaimStatusTransitions.FindManual(CaseStatus.Escalated, CaseStatus.InReview)!.RequiresSupervisor);
        Assert.True(ClaimStatusTransitions.FindManual(CaseStatus.Closed, CaseStatus.InReview)!.RequiresSupervisor);
        Assert.False(ClaimStatusTransitions.FindManual(CaseStatus.ReadyForReview, CaseStatus.InReview)!.RequiresSupervisor);
    }

    [Fact]
    public void NothingMovesToTheSameStatusAndEveryMoveIsListedOnce()
    {
        Assert.DoesNotContain(ClaimStatusTransitions.All, transition => transition.From == transition.To);
        var manual = ClaimStatusTransitions.All.Where(transition => transition.Path == StatusChangePath.ManualChange).ToArray();
        Assert.Equal(manual.Length, manual.Select(transition => (transition.From, transition.To)).Distinct().Count());
    }

    [Fact]
    public void AnOfficerCanWorkAClaimBackAndForthBetweenReviewMissingDocumentsAndReady()
    {
        foreach (var (from, to) in new[]
        {
            (CaseStatus.InReview, CaseStatus.MissingDocuments),
            (CaseStatus.MissingDocuments, CaseStatus.InReview),
            (CaseStatus.InReview, CaseStatus.ReadyForReview),
            (CaseStatus.ReadyForReview, CaseStatus.InReview),
            (CaseStatus.MissingDocuments, CaseStatus.ReadyForReview),
            (CaseStatus.ReadyForReview, CaseStatus.MissingDocuments),
        })
        {
            Assert.NotNull(ClaimStatusTransitions.FindManual(from, to));
        }
    }
}
