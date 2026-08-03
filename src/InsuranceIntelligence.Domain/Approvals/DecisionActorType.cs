namespace InsuranceIntelligence.Domain.Approvals;

/// <summary>
/// Actor kinds that may attempt a consequential Decide / Send / Execute.
/// Only <see cref="Human"/> may finalize when human review is required.
/// </summary>
public enum DecisionActorType
{
    Human,
    Agent,
    System,
    Integration
}
