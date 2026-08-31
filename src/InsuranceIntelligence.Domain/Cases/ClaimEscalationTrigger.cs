namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// High-risk stop conditions. Automation may prepare/queue; designated humans decide.
/// </summary>
public enum ClaimEscalationTrigger
{
    InjuryFatality,
    ContestedLiability,
    FraudSignal,
    SettlementOrPayment,
    Repudiation,
    TheftTotalLossSettlementRisk
}
