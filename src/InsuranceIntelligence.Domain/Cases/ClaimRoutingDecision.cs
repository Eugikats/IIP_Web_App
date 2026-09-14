using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// Optional runtime signals that force designated-human escalation on any claim type.
/// Fraud and contested liability must remain contestable operator signals, never auto-verdicts.
/// </summary>
public sealed record ClaimRoutingSignals(
    bool ContestedLiability = false,
    bool FraudSignalPresent = false,
    bool SettlementOrPaymentRequested = false,
    bool RepudiationRequested = false);

public sealed record ClaimRoutingDecision(
    ClaimTypeKind ClaimTypeKind,
    ClaimPathMode PathMode,
    CaseStatus InitialStatus,
    string AssignedTeam,
    CasePriority Priority,
    int SlaDays,
    string InitialRiskLevel,
    bool RequiresDesignatedHumanEscalation,
    IReadOnlyList<ClaimEscalationTrigger> EscalationTriggers,
    bool AllowsStandardReviewProgression,
    bool RequiresDesignatedHumanBeforeExport,
    string OperatorExplanation);
