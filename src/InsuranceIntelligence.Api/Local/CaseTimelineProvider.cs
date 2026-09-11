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

    // WO 5.6: "every step across every channel in one place" only holds if every real audit
    // action this codebase actually writes for a case has a real label here — a case timeline
    // that falls back to "A case event was recorded" for, say, every WhatsApp or email event
    // would silently hide exactly the cross-channel activity this screen exists to show. Kept in
    // the rough chronological order a claim's life touches them: intake -> documents -> customer
    // channels -> workflow/approvals -> customer updates -> claimant portal -> export.
    private static string SafeSummary(AuditEvent auditEvent) =>
        auditEvent.ActionName switch
        {
            "case.created" => "Case intake was created.",
            "case.assigned" => "The case was assigned to an owner or team.",
            "case.escalated" => "The case was escalated to a designated human reviewer.",
            "case.confirmed" => "A provisional, channel-created claim was confirmed by a human.",
            "case.status.updated" => "The case's lifecycle status was updated.",
            "case.completeness.updated" => "Document completeness score was updated.",

            "document.uploaded" => "A case document was uploaded.",
            "document.processed" => "A case document was processed in local mock mode.",
            "document.reclassified" => "A case document's type was reclassified.",
            "document.extraction.approved" => "Extracted document fields were approved.",
            "document.extraction.field_corrected" => "An extracted document field was corrected by a human.",

            "email.case_created" => "A claim was created from an inbound email.",
            "email.case_linked" => "An inbound email was linked to this claim.",
            "email.attachment_processed" => "An email attachment was processed onto this claim.",
            "email.attachment_processing_failed" => "An email attachment failed to process.",
            "email.received" => "An inbound email was received.",

            "whatsapp.outbound.attempted" => "An outbound WhatsApp message was sent for this claim.",
            "customer_assistant.autoreplied" => "The WhatsApp assistant auto-replied with a grounded, cited answer.",
            "customer_assistant.queuedforhumanapproval" => "The WhatsApp assistant drafted an answer that was queued for human approval, not auto-sent.",
            "customer_assistant.caseopened" => "The WhatsApp assistant opened this claim from an inbound message.",
            "customer_assistant.leadcreated" => "The WhatsApp assistant captured a sales lead from an inbound message.",
            "customer_assistant.escalated" => "The WhatsApp assistant escalated an inbound message to a human.",
            "customer_assistant.awaitingmoreinformation" => "The WhatsApp assistant asked the customer for more information.",
            "customer_assistant.declined" => "The WhatsApp assistant declined to act on an inbound message.",

            "fnol.acknowledgement" => "An automatic claim-received acknowledgement was attempted.",
            "missing_document_request" => "An automatic missing-document request was attempted.",

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
            "approval.next_step.executed" => "The approved next step was executed, advancing the case.",

            "customer_update.drafted" => "A customer-safe update draft was prepared.",
            "customer_update.approved" => "A customer update draft was approved.",
            "customer_update.rejected" => "A customer update draft was rejected.",
            "customer_update.sent" => "A customer update was sent through the simulated local provider.",

            "claim.review_requested" => "A claim review was requested.",
            "claim.review_acknowledged" => "A claim review request was acknowledged.",
            "claim.review_resolved" => "A claim review request was resolved.",
            "case_review.generated" => "An AI-assisted case review summary was generated.",

            "client_portal.magic_link.requested" => "A claimant portal magic link was requested.",
            "client_portal.session.established" => "The claimant signed in to the client portal.",

            "evidence_pack.exported" => "An evidence pack was exported for this case.",

            _ => "A case event was recorded."
        };
}
