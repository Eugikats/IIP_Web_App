using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests.Cases;

/// <summary>
/// Seam: claim-type routing and escalation policy for the single governed motor path.
/// </summary>
public sealed class ClaimRoutingPolicyTests
{
    [Theory]
    [InlineData("Own damage", ClaimTypeKind.OwnDamage)]
    [InlineData("Accident", ClaimTypeKind.OwnDamage)]
    [InlineData("Windscreen", ClaimTypeKind.OwnDamage)]
    [InlineData("Third-party property", ClaimTypeKind.ThirdPartyProperty)]
    [InlineData("TPPD", ClaimTypeKind.ThirdPartyProperty)]
    [InlineData("Theft", ClaimTypeKind.TheftTotalLoss)]
    [InlineData("Total loss", ClaimTypeKind.TheftTotalLoss)]
    [InlineData("Bodily injury", ClaimTypeKind.InjuryFatality)]
    [InlineData("Fatality", ClaimTypeKind.InjuryFatality)]
    public void Classify_Maps_Release1_Claim_Types(string claimType, ClaimTypeKind expected)
    {
        Assert.Equal(expected, ClaimTypeClassifier.Classify(claimType));
    }

    [Fact]
    public void Injury_Indicator_Forces_InjuryFatality_Even_When_ClaimType_Is_OwnDamage()
    {
        var motor = new MotorFnolDetails(null, null, null, null, null, null, "Yes - driver injured", null, null, null);

        Assert.Equal(ClaimTypeKind.InjuryFatality, ClaimTypeClassifier.Classify("Own damage", motor));
    }

    [Fact]
    public void OwnDamage_Enters_Happy_Path_Without_Mandatory_Escalation()
    {
        var decision = ClaimRoutingPolicy.Evaluate("Own damage");

        Assert.Equal(ClaimTypeKind.OwnDamage, decision.ClaimTypeKind);
        Assert.Equal(ClaimPathMode.HappyPath, decision.PathMode);
        Assert.Equal(CaseStatus.New, decision.InitialStatus);
        Assert.Equal(ClaimRoutingPolicy.ClaimsIntakeTeam, decision.AssignedTeam);
        Assert.False(decision.RequiresDesignatedHumanEscalation);
        Assert.True(decision.AllowsStandardReviewProgression);
        Assert.False(decision.RequiresDesignatedHumanBeforeExport);
    }

    [Fact]
    public void ThirdPartyProperty_Uses_Same_Path_With_ThirdParty_Queue()
    {
        var decision = ClaimRoutingPolicy.Evaluate("Third-party property damage");

        Assert.Equal(ClaimTypeKind.ThirdPartyProperty, decision.ClaimTypeKind);
        Assert.Equal(ClaimPathMode.HappyPath, decision.PathMode);
        Assert.Equal(ClaimRoutingPolicy.ThirdPartyDeskTeam, decision.AssignedTeam);
        Assert.False(decision.RequiresDesignatedHumanEscalation);
    }

    [Fact]
    public void TheftTotalLoss_Requires_Designated_Human_Before_Export()
    {
        var decision = ClaimRoutingPolicy.Evaluate("Theft / total loss");

        Assert.Equal(ClaimTypeKind.TheftTotalLoss, decision.ClaimTypeKind);
        Assert.Equal(ClaimPathMode.ExceptionRoute, decision.PathMode);
        Assert.Equal(ClaimRoutingPolicy.TotalLossDeskTeam, decision.AssignedTeam);
        Assert.Equal(CasePriority.High, decision.Priority);
        Assert.True(decision.RequiresDesignatedHumanEscalation);
        Assert.True(decision.RequiresDesignatedHumanBeforeExport);
        Assert.Contains(ClaimEscalationTrigger.TheftTotalLossSettlementRisk, decision.EscalationTriggers);
        Assert.False(decision.AllowsStandardReviewProgression);
    }

    [Fact]
    public void InjuryFatality_Starts_Escalated_On_Designated_Team()
    {
        var decision = ClaimRoutingPolicy.Evaluate("Motor bodily injury");

        Assert.Equal(ClaimTypeKind.InjuryFatality, decision.ClaimTypeKind);
        Assert.Equal(ClaimPathMode.ExceptionRoute, decision.PathMode);
        Assert.Equal(CaseStatus.Escalated, decision.InitialStatus);
        Assert.Equal(ClaimRoutingPolicy.DesignatedInjuryEscalationTeam, decision.AssignedTeam);
        Assert.Equal(CasePriority.Urgent, decision.Priority);
        Assert.Contains(ClaimEscalationTrigger.InjuryFatality, decision.EscalationTriggers);
    }

    [Fact]
    public void Classify_ReturnsUnknown_ForNonMotorProductLine_EvenWhenClaimTypeMatchesMotorKeywords()
    {
        // WO-15: the own-damage/third-party/theft/injury taxonomy is motor-specific. Before this
        // fix, a Medical claim's type text (routinely containing "medical") matched the
        // InjuryFatality keyword list and force-escalated every medical claim regardless of
        // severity, proven below via ClaimRoutingPolicy.Evaluate too.
        Assert.Equal(ClaimTypeKind.InjuryFatality, ClaimTypeClassifier.Classify("Medical reimbursement", productLine: "Motor"));
        Assert.Equal(ClaimTypeKind.Unknown, ClaimTypeClassifier.Classify("Medical reimbursement", productLine: "Medical"));
        Assert.Equal(ClaimTypeKind.InjuryFatality, ClaimTypeClassifier.Classify("Medical reimbursement"));
    }

    [Fact]
    public void MedicalClaim_DoesNotForceEscalate_EvenThoughClaimTypeTextContainsMedical()
    {
        var decision = ClaimRoutingPolicy.Evaluate("Medical reimbursement", productLine: "Medical");

        Assert.Equal(ClaimTypeKind.Unknown, decision.ClaimTypeKind);
        Assert.Equal(ClaimPathMode.HappyPath, decision.PathMode);
        Assert.Equal(CaseStatus.New, decision.InitialStatus);
        Assert.Equal(ClaimRoutingPolicy.ClaimsIntakeTeam, decision.AssignedTeam);
        Assert.Equal(CasePriority.Normal, decision.Priority);
        Assert.False(decision.RequiresDesignatedHumanEscalation);
    }

    [Theory]
    [InlineData(true, false, false, false, ClaimEscalationTrigger.ContestedLiability)]
    [InlineData(false, true, false, false, ClaimEscalationTrigger.FraudSignal)]
    [InlineData(false, false, true, false, ClaimEscalationTrigger.SettlementOrPayment)]
    [InlineData(false, false, false, true, ClaimEscalationTrigger.Repudiation)]
    public void HighRisk_Signals_Force_Designated_Human_Escalation(
        bool contested,
        bool fraud,
        bool settlement,
        bool repudiation,
        ClaimEscalationTrigger expected)
    {
        var decision = ClaimRoutingPolicy.Evaluate(
            "Own damage",
            signals: new ClaimRoutingSignals(contested, fraud, settlement, repudiation));

        Assert.Equal(ClaimPathMode.ExceptionRoute, decision.PathMode);
        Assert.True(decision.RequiresDesignatedHumanEscalation);
        Assert.Contains(expected, decision.EscalationTriggers);
        Assert.False(decision.AllowsStandardReviewProgression);
    }

    [Theory]
    [InlineData(DecisionActorType.Agent)]
    [InlineData(DecisionActorType.System)]
    [InlineData(DecisionActorType.Integration)]
    public void Ai_Cannot_Finalize_Consequential_Actions(DecisionActorType actorType)
    {
        foreach (ClaimConsequentialAction action in Enum.GetValues<ClaimConsequentialAction>())
        {
            var exception = Assert.Throws<DomainException>(() =>
                ClaimAuthorityPolicy.EnsureActorMayPerform(action, actorType));

            Assert.Contains("human", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Human_May_Finalize_Consequential_Actions()
    {
        var exception = Record.Exception(() =>
            ClaimAuthorityPolicy.EnsureActorMayPerform(
                ClaimConsequentialAction.ApproveSettlement,
                DecisionActorType.Human));

        Assert.Null(exception);
    }

    [Fact]
    public void Theft_Cannot_Advance_To_Export_Without_Designated_Path()
    {
        var claimCase = SampleCase("Theft", CaseStatus.ReadyForReview, ClaimRoutingPolicy.ClaimsIntakeTeam);

        var exception = Assert.Throws<DomainException>(() =>
            ClaimAuthorityPolicy.EnsureMayAdvanceToReadyForExport(claimCase, DecisionActorType.Human));

        Assert.Contains("designated-human escalation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Theft_May_Advance_To_Export_After_Designated_Human_Ownership()
    {
        var claimCase = SampleCase("Theft", CaseStatus.Escalated, ClaimRoutingPolicy.TotalLossDeskTeam);

        var exception = Record.Exception(() =>
            ClaimAuthorityPolicy.EnsureMayAdvanceToReadyForExport(claimCase, DecisionActorType.Human));

        Assert.Null(exception);
    }

    [Fact]
    public void Agent_Cannot_Advance_Injury_Case_Even_When_Escalated()
    {
        var claimCase = SampleCase("Bodily injury", CaseStatus.Escalated, ClaimRoutingPolicy.DesignatedInjuryEscalationTeam);

        var exception = Assert.Throws<DomainException>(() =>
            ClaimAuthorityPolicy.EnsureMayAdvanceToReadyForExport(claimCase, DecisionActorType.Agent));

        Assert.Contains("human", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_Requires_ReadyForExport_And_Human_Actor()
    {
        var notReady = SampleCase("Own damage", CaseStatus.PendingApproval, ClaimRoutingPolicy.ClaimsIntakeTeam);
        Assert.Throws<DomainException>(() =>
            ClaimAuthorityPolicy.EnsureMayExportEvidencePack(notReady, DecisionActorType.Human));

        var ready = SampleCase("Own damage", CaseStatus.ReadyForExport, ClaimRoutingPolicy.ClaimsIntakeTeam);
        Assert.Throws<DomainException>(() =>
            ClaimAuthorityPolicy.EnsureMayExportEvidencePack(ready, DecisionActorType.Agent));

        var exception = Record.Exception(() =>
            ClaimAuthorityPolicy.EnsureMayExportEvidencePack(ready, DecisionActorType.Human));
        Assert.Null(exception);
    }

    private static ClaimCase SampleCase(string claimType, CaseStatus status, string team) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CustomerName = "Amina Okello",
            PolicyNumber = "POL-1001",
            ProductLine = "Motor",
            ClaimType = claimType,
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Test incident",
            Status = status,
            CompletenessScore = 100m,
            AssignedTeam = team,
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-routing-1"
        };
}
