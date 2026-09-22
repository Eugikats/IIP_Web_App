namespace InsuranceIntelligence.Domain.Workflow;

/// <summary>
/// The single rule for whether any customer message, however it was created, needs approval before
/// it can be sent (outcome D: "Templates obey the same content checks and approval rules as any
/// other message and cannot be used to bypass them"). Previously duplicated inline in
/// MockCustomerUpdateDraftingProvider and AzureOpenAiCustomerUpdateDraftingProvider; now the one
/// place a manually written or templated message goes through too.
/// </summary>
public static class CustomerUpdateApprovalPolicy
{
    public static bool RequiresApproval(bool sensitive, CustomerUpdateReasonCategory reasonCategory) =>
        sensitive || reasonCategory is CustomerUpdateReasonCategory.ReadyForReview
            or CustomerUpdateReasonCategory.EscalationReceived
            or CustomerUpdateReasonCategory.DecisionOutcome;
}
