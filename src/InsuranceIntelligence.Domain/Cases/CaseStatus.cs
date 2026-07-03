namespace InsuranceIntelligence.Domain.Cases;

public enum CaseStatus
{
    New,
    MissingDocuments,
    ReadyForReview,
    Escalated,
    Settled,
    Closed
}
