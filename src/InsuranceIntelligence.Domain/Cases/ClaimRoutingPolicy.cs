using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// Single governed-path routing table. Claim type changes evidence, queue, and whether
/// the next step is standard review vs mandatory designated-human escalation.
/// AI never receives decision authority through this policy.
/// </summary>
public static class ClaimRoutingPolicy
{
    public const string ClaimsIntakeTeam = "Claims Intake";
    public const string ThirdPartyDeskTeam = "Third Party Desk";
    public const string TotalLossDeskTeam = "Total Loss Desk";
    public const string DesignatedInjuryEscalationTeam = "Designated Injury Escalation";
    public const string DesignatedFraudEscalationTeam = "Designated Fraud Escalation";
    public const string ClaimsSupervisionTeam = "Claims Supervision";

    public static ClaimRoutingDecision Evaluate(
        string? claimType,
        MotorFnolDetails? motorFnolDetails = null,
        ClaimRoutingSignals? signals = null)
    {
        var kind = ClaimTypeClassifier.Classify(claimType, motorFnolDetails);
        var activeSignals = signals ?? new ClaimRoutingSignals();
        var triggers = new List<ClaimEscalationTrigger>();

        if (kind == ClaimTypeKind.InjuryFatality)
        {
            triggers.Add(ClaimEscalationTrigger.InjuryFatality);
        }

        if (kind == ClaimTypeKind.TheftTotalLoss)
        {
            triggers.Add(ClaimEscalationTrigger.TheftTotalLossSettlementRisk);
        }

        if (activeSignals.ContestedLiability || IndicatesContestedLiability(claimType))
        {
            triggers.Add(ClaimEscalationTrigger.ContestedLiability);
        }

        if (activeSignals.FraudSignalPresent)
        {
            triggers.Add(ClaimEscalationTrigger.FraudSignal);
        }

        if (activeSignals.SettlementOrPaymentRequested)
        {
            triggers.Add(ClaimEscalationTrigger.SettlementOrPayment);
        }

        if (activeSignals.RepudiationRequested)
        {
            triggers.Add(ClaimEscalationTrigger.Repudiation);
        }

        var distinctTriggers = triggers.Distinct().ToArray();
        var requiresEscalation = distinctTriggers.Length > 0;
        var pathMode = kind is ClaimTypeKind.InjuryFatality or ClaimTypeKind.TheftTotalLoss || requiresEscalation
            ? ClaimPathMode.ExceptionRoute
            : ClaimPathMode.HappyPath;

        return kind switch
        {
            ClaimTypeKind.OwnDamage when !requiresEscalation => HappyOwnDamage(),
            ClaimTypeKind.ThirdPartyProperty when !requiresEscalation => HappyThirdParty(),
            ClaimTypeKind.TheftTotalLoss => TheftTotalLoss(distinctTriggers),
            ClaimTypeKind.InjuryFatality => InjuryFatality(distinctTriggers),
            _ when requiresEscalation => ExceptionUnknown(kind, distinctTriggers),
            ClaimTypeKind.OwnDamage => ApplySignalOverride(HappyOwnDamage(), distinctTriggers),
            ClaimTypeKind.ThirdPartyProperty => ApplySignalOverride(HappyThirdParty(), distinctTriggers),
            _ => UnknownDefault(distinctTriggers)
        };
    }

    public static ClaimRoutingDecision Evaluate(ClaimCase claimCase, ClaimRoutingSignals? signals = null) =>
        Evaluate(claimCase.ClaimType, claimCase.MotorFnolDetails, signals);

    public static string ResolveEscalationTeam(ClaimRoutingDecision decision)
    {
        if (decision.EscalationTriggers.Contains(ClaimEscalationTrigger.FraudSignal))
        {
            return DesignatedFraudEscalationTeam;
        }

        if (decision.EscalationTriggers.Contains(ClaimEscalationTrigger.InjuryFatality))
        {
            return DesignatedInjuryEscalationTeam;
        }

        if (decision.EscalationTriggers.Contains(ClaimEscalationTrigger.TheftTotalLossSettlementRisk)
            || decision.EscalationTriggers.Contains(ClaimEscalationTrigger.SettlementOrPayment)
            || decision.EscalationTriggers.Contains(ClaimEscalationTrigger.Repudiation))
        {
            return TotalLossDeskTeam;
        }

        if (decision.EscalationTriggers.Contains(ClaimEscalationTrigger.ContestedLiability))
        {
            return ClaimsSupervisionTeam;
        }

        return decision.AssignedTeam;
    }

    private static ClaimRoutingDecision HappyOwnDamage() =>
        new(
            ClaimTypeKind.OwnDamage,
            ClaimPathMode.HappyPath,
            CaseStatus.New,
            ClaimsIntakeTeam,
            CasePriority.Normal,
            SlaDays: 3,
            InitialRiskLevel: "Low",
            RequiresDesignatedHumanEscalation: false,
            EscalationTriggers: Array.Empty<ClaimEscalationTrigger>(),
            AllowsStandardReviewProgression: true,
            RequiresDesignatedHumanBeforeExport: false,
            OperatorExplanation: "Non-injury motor own-damage follows the Release 1 happy path with human review and approval gates.");

    private static ClaimRoutingDecision HappyThirdParty() =>
        new(
            ClaimTypeKind.ThirdPartyProperty,
            ClaimPathMode.HappyPath,
            CaseStatus.New,
            ThirdPartyDeskTeam,
            CasePriority.Normal,
            SlaDays: 3,
            InitialRiskLevel: "Low",
            RequiresDesignatedHumanEscalation: false,
            EscalationTriggers: Array.Empty<ClaimEscalationTrigger>(),
            AllowsStandardReviewProgression: true,
            RequiresDesignatedHumanBeforeExport: false,
            OperatorExplanation: "Third-party property damage uses the same governed path with multi-party evidence and the third-party desk queue.");

    private static ClaimRoutingDecision TheftTotalLoss(IReadOnlyList<ClaimEscalationTrigger> triggers) =>
        new(
            ClaimTypeKind.TheftTotalLoss,
            ClaimPathMode.ExceptionRoute,
            CaseStatus.New,
            TotalLossDeskTeam,
            CasePriority.High,
            SlaDays: 2,
            InitialRiskLevel: "High",
            RequiresDesignatedHumanEscalation: true,
            EscalationTriggers: triggers,
            AllowsStandardReviewProgression: false,
            RequiresDesignatedHumanBeforeExport: true,
            OperatorExplanation: "Theft/total-loss stays on the governed path but requires designated-human escalation before settlement-risk approval, customer adverse outcomes, or export. AI cannot close or pay.");

    private static ClaimRoutingDecision InjuryFatality(IReadOnlyList<ClaimEscalationTrigger> triggers) =>
        new(
            ClaimTypeKind.InjuryFatality,
            ClaimPathMode.ExceptionRoute,
            CaseStatus.Escalated,
            DesignatedInjuryEscalationTeam,
            CasePriority.Urgent,
            SlaDays: 1,
            InitialRiskLevel: "Critical",
            RequiresDesignatedHumanEscalation: true,
            EscalationTriggers: triggers,
            AllowsStandardReviewProgression: false,
            RequiresDesignatedHumanBeforeExport: true,
            OperatorExplanation: "Injury/fatality is an exception route: designated human escalation is mandatory. AI may prepare evidence only; it never determines outcome. Internal risk notes must not reach claimants.");

    private static ClaimRoutingDecision ExceptionUnknown(
        ClaimTypeKind kind,
        IReadOnlyList<ClaimEscalationTrigger> triggers) =>
        ApplySignalOverride(
            new ClaimRoutingDecision(
                kind,
                ClaimPathMode.ExceptionRoute,
                CaseStatus.Escalated,
                ClaimsSupervisionTeam,
                CasePriority.Urgent,
                SlaDays: 1,
                InitialRiskLevel: "High",
                RequiresDesignatedHumanEscalation: true,
                EscalationTriggers: triggers,
                AllowsStandardReviewProgression: false,
                RequiresDesignatedHumanBeforeExport: true,
                OperatorExplanation: "Contested liability, fraud, settlement/payment, or repudiation signals require designated-human escalation before consequential progression."),
            triggers);

    private static ClaimRoutingDecision UnknownDefault(IReadOnlyList<ClaimEscalationTrigger> triggers) =>
        triggers.Count == 0
            ? new ClaimRoutingDecision(
                ClaimTypeKind.Unknown,
                ClaimPathMode.HappyPath,
                CaseStatus.New,
                ClaimsIntakeTeam,
                CasePriority.Normal,
                SlaDays: 3,
                InitialRiskLevel: "Low",
                RequiresDesignatedHumanEscalation: false,
                EscalationTriggers: Array.Empty<ClaimEscalationTrigger>(),
                AllowsStandardReviewProgression: true,
                RequiresDesignatedHumanBeforeExport: false,
                OperatorExplanation: "Unrecognized claim type remains on the governed path with standard human gates until classified.")
            : ExceptionUnknown(ClaimTypeKind.Unknown, triggers);

    private static ClaimRoutingDecision ApplySignalOverride(
        ClaimRoutingDecision baseline,
        IReadOnlyList<ClaimEscalationTrigger> triggers)
    {
        var team = ResolveEscalationTeam(baseline with { EscalationTriggers = triggers });
        var injury = triggers.Contains(ClaimEscalationTrigger.InjuryFatality);
        return baseline with
        {
            PathMode = ClaimPathMode.ExceptionRoute,
            InitialStatus = injury ? CaseStatus.Escalated : baseline.InitialStatus,
            AssignedTeam = team,
            Priority = injury || triggers.Contains(ClaimEscalationTrigger.FraudSignal)
                ? CasePriority.Urgent
                : CasePriority.High,
            SlaDays = Math.Min(baseline.SlaDays, 2),
            InitialRiskLevel = injury || triggers.Contains(ClaimEscalationTrigger.FraudSignal) ? "Critical" : "High",
            RequiresDesignatedHumanEscalation = true,
            EscalationTriggers = triggers,
            AllowsStandardReviewProgression = false,
            RequiresDesignatedHumanBeforeExport = true,
            OperatorExplanation = baseline.OperatorExplanation
                + " Designated-human escalation is required because of: "
                + string.Join(", ", triggers)
                + ". Fraud and contested-liability signals are contestable operator findings, not final determinations."
        };
    }

    private static bool IndicatesContestedLiability(string? claimType)
    {
        var normalized = string.IsNullOrWhiteSpace(claimType)
            ? string.Empty
            : claimType.Trim().ToLowerInvariant();
        return normalized.Contains("contested", StringComparison.Ordinal)
            || normalized.Contains("liability dispute", StringComparison.Ordinal)
            || normalized.Contains("disputed liability", StringComparison.Ordinal);
    }
}
