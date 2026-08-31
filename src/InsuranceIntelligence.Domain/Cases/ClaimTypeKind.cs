namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// Release 1 motor claim-type discriminator on the single governed path.
/// Injury/fatality and theft/total-loss are exception routes, not separate engines.
/// </summary>
public enum ClaimTypeKind
{
    OwnDamage,
    ThirdPartyProperty,
    TheftTotalLoss,
    InjuryFatality,
    Unknown
}
