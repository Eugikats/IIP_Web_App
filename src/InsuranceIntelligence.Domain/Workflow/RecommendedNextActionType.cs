namespace InsuranceIntelligence.Domain.Workflow;

public enum RecommendedNextActionType
{
    RequestMissingDocuments,
    MarkReadyForReview,
    SupervisorReview,
    HumanReview,
    FollowUpCustomer
}
