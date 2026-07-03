using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

public sealed class CaseTimelineProvider : ICaseTimelineProvider
{
    public IReadOnlyCollection<CaseTimelineEvent> Build(Guid caseId, IReadOnlyCollection<AuditEvent> auditEvents) =>
        auditEvents
            .Where(auditEvent => auditEvent.CaseId == caseId)
            .OrderBy(auditEvent => auditEvent.Timestamp)
            .Select(auditEvent => new CaseTimelineEvent(
                auditEvent.EventId,
                caseId,
                auditEvent.ActionName,
                auditEvent.ActorType.ToString(),
                auditEvent.Timestamp,
                SafeSummary(auditEvent),
                auditEvent.BeforeStatus,
                auditEvent.AfterStatus,
                auditEvent.CorrelationId))
            .ToArray();

    private static string SafeSummary(AuditEvent auditEvent) =>
        auditEvent.ActionName switch
        {
            "case.created" => "Case intake was created.",
            "document.uploaded" => "A case document was uploaded.",
            "document.processed" => "A case document was processed in local mock mode.",
            "case.completeness.updated" => "Document completeness score was updated.",
            "assistant.policy_question.answered" => "Policy assistant returned a grounded answer or escalation.",
            "workflow.evaluated" => "Workflow rules were evaluated.",
            "workflow.task.created" => "A workflow task was created.",
            "workflow.task.assigned" => "A workflow task was assigned.",
            "workflow.task.completed" => "A workflow task was completed.",
            "approval.created" => "A human approval request was created.",
            "approval.approved" => "A human reviewer approved the workflow request.",
            "approval.rejected" => "A human reviewer rejected the workflow request.",
            "approval.overridden" => "A human reviewer recorded an override.",
            "approval.escalated" => "A human reviewer escalated the workflow request.",
            "customer_update.drafted" => "A customer-safe update draft was prepared.",
            "customer_update.approved" => "A customer update draft was approved.",
            "customer_update.rejected" => "A customer update draft was rejected.",
            "customer_update.sent" => "A customer update was sent through the simulated local provider.",
            _ => "A case event was recorded."
        };
}
