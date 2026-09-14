namespace InsuranceIntelligence.Domain.Workflow;

public enum CustomerUpdateReasonCategory
{
    ClaimReceived,
    MissingDocuments,
    ReadyForReview,
    StatusUpdate,
    EscalationReceived,

    /// <summary>A human has decided the claim's outcome (blueprint §14 Phase 5, WO 5.5). Always
    /// requires approval before sending (see both ICustomerUpdateDraftingProvider implementations)
    ///, and the draft itself must never state the actual decision. AGENTS.md bars AI from
    /// communicating final claim decisions; the Azure provider's own prompt already instructs
    /// "do not include ... final claim decisions," so a drafted message for this category tells
    /// the customer a decision exists and to expect contact, never what the decision was. The
    /// decision itself is delivered by the approving human, not drafted by the model.</summary>
    DecisionOutcome
}
