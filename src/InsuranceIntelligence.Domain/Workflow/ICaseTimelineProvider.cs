using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Domain.Workflow;

public interface ICaseTimelineProvider
{
    IReadOnlyCollection<CaseTimelineEvent> Build(Guid caseId, IReadOnlyCollection<AuditEvent> auditEvents);
}
