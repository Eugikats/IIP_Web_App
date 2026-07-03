namespace InsuranceIntelligence.Domain.Workflow;

public enum WorkflowTaskType
{
    RequestMissingDocuments,
    ReviewCase,
    SupervisorApproval,
    CustomerUpdateApproval,
    ComplianceReview,
    FollowUpCustomer
}
