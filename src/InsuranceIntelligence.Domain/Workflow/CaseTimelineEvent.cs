namespace InsuranceIntelligence.Domain.Workflow;

public sealed record CaseTimelineEvent(
    Guid EventId,
    Guid CaseId,
    string EventType,
    string Actor,
    DateTimeOffset Timestamp,
    string Summary,
    string? BeforeStatus,
    string? AfterStatus,
    string CorrelationId);
