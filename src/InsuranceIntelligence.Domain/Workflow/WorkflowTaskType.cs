namespace InsuranceIntelligence.Domain.Workflow;

public enum WorkflowTaskType
{
    RequestMissingDocuments,
    ReviewCase,
    SupervisorApproval,
    CustomerUpdateApproval,
    ComplianceReview,
    FollowUpCustomer,

    /// <summary>Outcome O: opened automatically when a closed claim's satisfaction answer is
    /// recorded as Poor, so a supervisor follows up rather than the poor experience going unseen.</summary>
    ServiceRecoveryFollowUp
}
