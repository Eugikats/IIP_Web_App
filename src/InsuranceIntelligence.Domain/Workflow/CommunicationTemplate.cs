namespace InsuranceIntelligence.Domain.Workflow;

public sealed record CommunicationTemplate(
    CustomerUpdateReasonCategory ReasonCategory,
    CommunicationChannel Channel,
    string Subject,
    string Body);
