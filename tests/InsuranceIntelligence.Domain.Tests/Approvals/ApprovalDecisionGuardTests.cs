using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests.Approvals;

/// <summary>
/// Seam: platform ceiling — non-human actors cannot Decide/Send/Execute consequential actions.
/// </summary>
public sealed class ApprovalDecisionGuardTests
{
    [Theory]
    [InlineData(DecisionActorType.Agent)]
    [InlineData(DecisionActorType.System)]
    [InlineData(DecisionActorType.Integration)]
    public void EnsureHumanForConsequentialDecision_Throws_For_NonHuman(DecisionActorType actorType)
    {
        var exception = Assert.Throws<DomainException>(() =>
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
                requiresHumanDecision: true,
                actorType,
                requestedActionType: "approve"));

        Assert.Contains("human", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actorType.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureHumanForConsequentialDecision_Allows_Human()
    {
        var exception = Record.Exception(() =>
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
                requiresHumanDecision: true,
                DecisionActorType.Human,
                requestedActionType: "approve"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureHumanForConsequentialDecision_Skips_When_Not_Required()
    {
        var exception = Record.Exception(() =>
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
                requiresHumanDecision: false,
                DecisionActorType.Agent,
                requestedActionType: "low-risk-status"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureReasonProvided_Throws_For_Blank_Comments(string? comments)
    {
        var exception = Assert.Throws<DomainException>(() => ApprovalDecisionGuard.EnsureReasonProvided(comments));

        Assert.Contains("comments are required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureReasonProvided_Allows_A_Real_Reason()
    {
        var exception = Record.Exception(() => ApprovalDecisionGuard.EnsureReasonProvided("Reviewed the evidence pack and confirmed cover applies."));

        Assert.Null(exception);
    }
}
