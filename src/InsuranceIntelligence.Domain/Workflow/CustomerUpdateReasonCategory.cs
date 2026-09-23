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
    DecisionOutcome,

    /// <summary>Outcome O: the one satisfaction question asked after a claim closes. Always
    /// requires approval (see CustomerUpdateApprovalPolicy) and, like any other customer message,
    /// is blocked without recorded consent unless the tenant has opted into low-risk auto-send.</summary>
    SatisfactionQuestion
}
