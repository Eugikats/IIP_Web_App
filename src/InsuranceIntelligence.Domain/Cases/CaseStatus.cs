namespace InsuranceIntelligence.Domain.Cases;

public enum CaseStatus
{
    New,
    Assigned,
    InReview,
    MissingDocuments,
    ReadyForReview,
    PendingApproval,
    ReadyForExport,
    Escalated,
    Settled,
    Closed
}
