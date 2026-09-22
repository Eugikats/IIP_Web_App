using InsuranceIntelligence.Application.MyWork;

namespace InsuranceIntelligence.Domain.Tests.MyWork;

public sealed class MyWorkRankingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OverdueItem_ScoresHigherThanOnTrackItem()
    {
        var (overdueScore, overdueReason) = MyWorkRanking.Explain(
            MyWorkDueState.Overdue, MyWorkEvidenceReadiness.Ready, false, MyWorkState.Ready, "Normal",
            isAssigned: true, updatedAt: Now, now: Now);
        var (onTrackScore, _) = MyWorkRanking.Explain(
            MyWorkDueState.OnTrack, MyWorkEvidenceReadiness.Ready, false, MyWorkState.Ready, "Normal",
            isAssigned: true, updatedAt: Now, now: Now);

        Assert.True(overdueScore > onTrackScore);
        Assert.Equal("Past its due date.", overdueReason);
    }

    [Fact]
    public void UnassignedItem_IsFlaggedFirstInTheReason()
    {
        var (score, reason) = MyWorkRanking.Explain(
            MyWorkDueState.Overdue, MyWorkEvidenceReadiness.Ready, false, MyWorkState.Ready, "Normal",
            isAssigned: false, updatedAt: Now, now: Now);

        Assert.True(score > 0);
        Assert.StartsWith("Not yet assigned to anyone", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void EscalatedItem_OutranksAReadyItemWithNoOtherSignal()
    {
        var (escalatedScore, escalatedReason) = MyWorkRanking.Explain(
            MyWorkDueState.NoDueDate, MyWorkEvidenceReadiness.Ready, false, MyWorkState.Escalated, "Normal",
            isAssigned: true, updatedAt: Now, now: Now);
        var (readyScore, _) = MyWorkRanking.Explain(
            MyWorkDueState.NoDueDate, MyWorkEvidenceReadiness.Ready, false, MyWorkState.Ready, "Normal",
            isAssigned: true, updatedAt: Now, now: Now);

        Assert.True(escalatedScore > readyScore);
        Assert.Contains("Escalated and needs a decision", escalatedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSignalsPresent_FallsBackToAnHonestDefaultReason()
    {
        var (score, reason) = MyWorkRanking.Explain(
            MyWorkDueState.NoDueDate, MyWorkEvidenceReadiness.Ready, false, MyWorkState.InProgress, "Normal",
            isAssigned: true, updatedAt: Now, now: Now);

        Assert.Equal(0m, score);
        Assert.Equal("Open and awaiting your review", reason);
    }

    [Fact]
    public void LongWaitingItem_MentionsHowLongItHasSatOnlyAtThreeDaysOrMore()
    {
        var (_, twoDayReason) = MyWorkRanking.Explain(
            MyWorkDueState.NoDueDate, MyWorkEvidenceReadiness.Ready, false, MyWorkState.Ready, "Normal",
            isAssigned: true, updatedAt: Now.AddDays(-2), now: Now);
        var (_, fiveDayReason) = MyWorkRanking.Explain(
            MyWorkDueState.NoDueDate, MyWorkEvidenceReadiness.Ready, false, MyWorkState.Ready, "Normal",
            isAssigned: true, updatedAt: Now.AddDays(-5), now: Now);

        Assert.DoesNotContain("Waiting", twoDayReason, StringComparison.Ordinal);
        Assert.Contains("Waiting 5 days since it last moved", fiveDayReason, StringComparison.Ordinal);
    }
}
