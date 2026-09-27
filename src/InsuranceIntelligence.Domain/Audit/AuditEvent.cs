using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Audit;

public sealed record AuditEvent(
    Guid EventId,
    TenantScope Scope,
    Guid? CaseId,
    string? DocumentId,
    string? UserId,
    ActorType ActorType,
    string ActionName,
    DateTimeOffset Timestamp,
    string? BeforeStatus,
    string? AfterStatus,
    IReadOnlyCollection<string> EvidenceIds,
    string? ModelProvider,
    decimal? Confidence,
    string ValidationResult,
    string? ApprovalId,
    string? AuditReason,
    string CorrelationId)
{
    public static AuditEvent CaseCreated(ClaimCaseSnapshot snapshot, string userId, string correlationId) =>
        new(
            Guid.NewGuid(),
            snapshot.Scope,
            snapshot.CaseId,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "case.created",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: snapshot.Status,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Mock",
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent ClaimReviewRequested(ClaimReviewRequest request) =>
        new(
            Guid.NewGuid(),
            request.Scope,
            request.CaseId,
            DocumentId: null,
            UserId: request.RequestedByUserId,
            ActorType.User,
            "claim.review_requested",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: request.Status.ToString(),
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Contestability",
            Confidence: null,
            ValidationResult: request.Origin.ToString(),
            ApprovalId: null,
            AuditReason: request.Reason,
            CorrelationId: request.CorrelationId);

    public static AuditEvent ClaimReviewAcknowledged(
        ClaimReviewRequest request,
        string actorUserId,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            request.Scope,
            request.CaseId,
            DocumentId: null,
            UserId: actorUserId,
            ActorType.User,
            "claim.review_acknowledged",
            DateTimeOffset.UtcNow,
            BeforeStatus: ClaimReviewRequestStatus.Open.ToString(),
            AfterStatus: request.Status.ToString(),
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Contestability",
            Confidence: null,
            ValidationResult: "acknowledged",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent ClaimReviewResolved(
        ClaimReviewRequest request,
        string actorUserId,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            request.Scope,
            request.CaseId,
            DocumentId: null,
            UserId: actorUserId,
            ActorType.User,
            "claim.review_resolved",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: request.Status.ToString(),
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Contestability",
            Confidence: null,
            ValidationResult: "resolved",
            ApprovalId: null,
            AuditReason: request.ResolutionNote,
            CorrelationId: correlationId);

    public static AuditEvent CaseAssigned(ClaimCaseSnapshot before, ClaimCaseSnapshot after, string assignedByUserId, string correlationId, string? auditReason = null) =>
        new(
            Guid.NewGuid(),
            after.Scope,
            after.CaseId,
            DocumentId: null,
            UserId: assignedByUserId,
            ActorType.User,
            "case.assigned",
            DateTimeOffset.UtcNow,
            before.Status,
            after.Status,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Workflow",
            Confidence: null,
            ValidationResult: "assigned",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent CaseEscalated(ClaimCaseSnapshot before, ClaimCaseSnapshot after, string escalatedByUserId, string correlationId, string auditReason) =>
        new(
            Guid.NewGuid(),
            after.Scope,
            after.CaseId,
            DocumentId: null,
            UserId: escalatedByUserId,
            ActorType.User,
            "case.escalated",
            DateTimeOffset.UtcNow,
            before.Status,
            after.Status,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Workflow",
            Confidence: null,
            ValidationResult: "escalated",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent CaseConfirmed(ClaimCaseSnapshot before, ClaimCaseSnapshot after, string confirmedByUserId, string correlationId, string auditReason) =>
        new(
            Guid.NewGuid(),
            after.Scope,
            after.CaseId,
            DocumentId: null,
            UserId: confirmedByUserId,
            ActorType.User,
            "case.confirmed",
            DateTimeOffset.UtcNow,
            before.Status,
            after.Status,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "confirmed",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent CaseStatusUpdated(ClaimCaseSnapshot before, ClaimCaseSnapshot after, string userId, string correlationId, string auditReason) =>
        new(
            Guid.NewGuid(),
            after.Scope,
            after.CaseId,
            DocumentId: null,
            UserId: userId,
            ActorType.System,
            "case.status.updated",
            DateTimeOffset.UtcNow,
            before.Status,
            after.Status,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Workflow",
            Confidence: null,
            ValidationResult: "updated",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent DocumentUploaded(CaseDocument document, string userId, string correlationId) =>
        ForDocument(document, userId, ActorType.User, "document.uploaded", null, null, "accepted", correlationId);

    public static AuditEvent DocumentProcessed(CaseDocument document, string correlationId) =>
        ForDocument(
            document,
            userId: null,
            actorType: ActorType.Agent,
            actionName: "document.processed",
            BeforeStatus: DocumentProcessingStatus.Uploaded.ToString(),
            AfterStatus: document.ProcessingStatus.ToString(),
            ValidationResult: document.ValidationWarnings.Count == 0 ? "accepted" : "warnings",
            correlationId: correlationId,
            confidence: document.Classification.ConfidenceScore);

    /// <summary>A customer's "remove" or "replace" (claim intake build prompt outcome B): the
    /// document itself is never deleted, only marked superseded, and this is its audit trail
    /// entry.</summary>
    public static AuditEvent DocumentSuperseded(CaseDocument document, string userId, string correlationId) =>
        ForDocument(document, userId, ActorType.User, "document.superseded", document.ProcessingStatus.ToString(), "Superseded", "accepted", correlationId);

    public static AuditEvent DocumentReclassified(CaseDocument document, string userId, string beforeStatus, string correlationId, string? auditReason = null) =>
        ForDocument(document, userId, ActorType.User, "document.reclassified", beforeStatus, document.DocumentType.ToString(), "accepted", correlationId, auditReason: auditReason);

    public static AuditEvent DocumentExtractionApproved(CaseDocument document, string userId, string correlationId, string? auditReason = null) =>
        ForDocument(document, userId, ActorType.User, "document.extraction.approved", null, document.ProcessingStatus.ToString(), "approved", correlationId, auditReason: auditReason);

    public static AuditEvent DocumentExtractionFieldCorrected(
        CaseDocument document,
        string userId,
        string fieldName,
        string correlationId,
        string auditReason) =>
        new(
            Guid.NewGuid(),
            document.Scope,
            document.CaseId,
            document.DocumentId.ToString(),
            userId,
            ActorType.User,
            "document.extraction.field_corrected",
            DateTimeOffset.UtcNow,
            BeforeStatus: fieldName,
            AfterStatus: "corrected",
            EvidenceIds: [document.DocumentId.ToString(), fieldName],
            ModelProvider: document.Extraction.ProviderName,
            Confidence: null,
            ValidationResult: "corrected",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent CaseCompletenessUpdated(
        ClaimCaseSnapshot before,
        ClaimCaseSnapshot after,
        decimal completenessScore,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            after.Scope,
            after.CaseId,
            DocumentId: null,
            UserId: null,
            ActorType.System,
            "case.completeness.updated",
            DateTimeOffset.UtcNow,
            BeforeStatus: before.Status,
            AfterStatus: after.Status,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Mock",
            Confidence: completenessScore,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent KnowledgeUploaded(KnowledgeDocument document, string userId, string correlationId) =>
        ForKnowledge(document, userId, ActorType.User, "knowledge.uploaded", null, document.ApprovalStatus.ToString(), "accepted", correlationId);

    public static AuditEvent KnowledgeApproved(KnowledgeDocument document, string userId, string correlationId, string? auditReason = null) =>
        ForKnowledge(document, userId, ActorType.User, "knowledge.approved", KnowledgeDocumentStatus.Draft.ToString(), document.ApprovalStatus.ToString(), "approved", correlationId, auditReason);

    public static AuditEvent KnowledgeIndexed(KnowledgeDocument document, string correlationId) =>
        ForKnowledge(document, null, ActorType.System, "knowledge.indexed", KnowledgeIndexedStatus.NotIndexed.ToString(), document.IndexedStatus.ToString(), "indexed", correlationId);

    public static AuditEvent KnowledgeArchived(KnowledgeDocument document, string userId, string correlationId, string? auditReason = null) =>
        ForKnowledge(document, userId, ActorType.User, "knowledge.archived", KnowledgeDocumentStatus.Approved.ToString(), document.ApprovalStatus.ToString(), "archived", correlationId, auditReason);

    public static AuditEvent KnowledgeReclassified(KnowledgeDocument document, string userId, string correlationId, string beforeSensitivity, string? auditReason = null) =>
        ForKnowledge(document, userId, ActorType.User, "knowledge.reclassified", beforeSensitivity, document.Sensitivity.ToString(), "reclassified", correlationId, auditReason);

    public static AuditEvent KnowledgeReindexed(TenantScope scope, int indexedDocumentCount, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: null,
            ActorType.System,
            "knowledge.reindexed",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: "Indexed",
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "KnowledgeIndex",
            Confidence: indexedDocumentCount,
            ValidationResult: "indexed",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent DocumentFieldValidationRuleSaved(
        DocumentFieldValidationRule rule, string userId, string correlationId, string auditReason) =>
        new(
            Guid.NewGuid(),
            rule.Scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "document_field_validation_rule.saved",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: rule.Enabled ? "Enabled" : "Disabled",
            EvidenceIds: [rule.RuleId],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent ClaimantMagicLinkRequested(TenantScope scope, Guid? caseId, bool matched, string channel, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            caseId,
            DocumentId: null,
            UserId: null,
            ActorType.System,
            "client_portal.magic_link.requested",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            // Never let AfterStatus/logs distinguish "matched" from "not matched" in anything a
            // caller could observe over the API, this field exists only for staff investigating
            // abuse in the audit log, not as an enumeration side-channel.
            AfterStatus: matched ? "Issued" : "NoMatch",
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: channel,
            Confidence: null,
            ValidationResult: matched ? "issued" : "no_match",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent ClaimantSessionEstablished(ClaimantPortalSession session, string correlationId) =>
        new(
            Guid.NewGuid(),
            session.Scope,
            session.CaseId,
            DocumentId: null,
            UserId: session.ContactValue,
            ActorType.User,
            "client_portal.session.established",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: "Active",
            EvidenceIds: [session.SessionId.ToString()],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    /// <summary>Outcome L: "every disclosure is logged against the verified session." Written on
    /// every claimant-facing read (claim status, policy details) that would show a person
    /// something specific to their own policy or claim, not just on authentication events.
    /// <paramref name="disclosureType"/> names what was shown ("ClaimStatus", "Policy", ...).</summary>
    public static AuditEvent ClaimantDisclosureViewed(
        TenantScope scope,
        Guid? caseId,
        Guid sessionId,
        string contactValue,
        string disclosureType,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            caseId,
            DocumentId: null,
            UserId: contactValue,
            ActorType.User,
            "client_portal.disclosure.viewed",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: disclosureType,
            EvidenceIds: [sessionId.ToString()],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "disclosed",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent ContactDetailChangeRequested(ContactDetailChangeRequest request) =>
        new(
            Guid.NewGuid(),
            request.Scope,
            request.CaseId,
            DocumentId: null,
            UserId: request.RequestedByUserId,
            ActorType.User,
            "customer.contact_change_requested",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: request.Status.ToString(),
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "ClientPortal",
            Confidence: null,
            ValidationResult: request.PolicyNumber,
            ApprovalId: null,
            AuditReason: request.Note,
            CorrelationId: request.CorrelationId);

    public static AuditEvent ContactDetailChangeAcknowledged(
        ContactDetailChangeRequest request,
        string actorUserId,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            request.Scope,
            request.CaseId,
            DocumentId: null,
            UserId: actorUserId,
            ActorType.User,
            "customer.contact_change_acknowledged",
            DateTimeOffset.UtcNow,
            BeforeStatus: ContactDetailChangeStatus.Open.ToString(),
            AfterStatus: request.Status.ToString(),
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "ClientPortal",
            Confidence: null,
            ValidationResult: "acknowledged",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent ContactDetailChangeResolved(
        ContactDetailChangeRequest request,
        string actorUserId,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            request.Scope,
            request.CaseId,
            DocumentId: null,
            UserId: actorUserId,
            ActorType.User,
            "customer.contact_change_resolved",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: request.Status.ToString(),
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "ClientPortal",
            Confidence: null,
            ValidationResult: "resolved",
            ApprovalId: null,
            AuditReason: request.ResolutionNote,
            CorrelationId: correlationId);

    /// <summary>Outcome M: "visible... in the audit trail." One factory covers both a grant and a
    /// withdrawal, the two are the same shape (a ConsentRecord row), distinguished by
    /// record.WithdrawnAt, exactly the way the record itself distinguishes them.</summary>
    public static AuditEvent ConsentRecorded(ConsentRecord record) =>
        new(
            Guid.NewGuid(),
            record.Scope,
            CaseId: null,
            DocumentId: null,
            UserId: record.RecordedByUserId,
            ActorType.User,
            record.WithdrawnAt is null ? "consent.granted" : "consent.withdrawn",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: record.Basis.ToString(),
            EvidenceIds: record.InteractionReference is null ? Array.Empty<string>() : [record.InteractionReference],
            ModelProvider: record.Channel,
            Confidence: null,
            ValidationResult: record.Purpose,
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: record.CorrelationId);

    /// <summary>Outcome N: a file import is a bulk write with no per-row human review, so the
    /// batch itself needs its own audit trail entry, who ran it, how many rows in, how many
    /// actually landed as policies, distinct from any one imported record's own history.</summary>
    public static AuditEvent PolicyImportCommitted(
        TenantScope scope,
        string userId,
        int totalRows,
        int importedCount,
        int failedCount,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "policy.import.committed",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: $"imported:{importedCount};failed:{failedCount}",
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "PolicyImport",
            Confidence: null,
            ValidationResult: $"total:{totalRows}",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent RetentionReminderDrafted(
        TenantScope scope, string userId, Guid reminderId, string policyNumber, int expiryBucketDays, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "retention.reminder.drafted",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: "PendingApproval",
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: null,
            Confidence: null,
            ValidationResult: $"reminderId:{reminderId};policyNumber:{policyNumber};expiryBucketDays:{expiryBucketDays}",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent RetentionReminderSent(
        TenantScope scope, string userId, Guid reminderId, string policyNumber, string deliveryStatus, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "retention.reminder.sent",
            DateTimeOffset.UtcNow,
            BeforeStatus: "PendingApproval",
            AfterStatus: deliveryStatus,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: null,
            Confidence: null,
            ValidationResult: $"reminderId:{reminderId};policyNumber:{policyNumber}",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent SatisfactionResponseRecorded(
        TenantScope scope, Guid caseId, string userId, string rating, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: caseId,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "claim.satisfaction.recorded",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: rating,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "recorded",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent ServiceRecoveryTaskOpened(
        TenantScope scope, Guid caseId, string userId, Guid taskId, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: caseId,
            DocumentId: null,
            UserId: userId,
            ActorType.System,
            "claim.service_recovery.opened",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: "Open",
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: null,
            Confidence: null,
            ValidationResult: $"taskId:{taskId}",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);

    public static AuditEvent WhatsAppConfigurationSaved(
        TenantScope scope, string userId, string phoneNumberId, string auditReason, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "integration.whatsapp.configuration_saved",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: "Connected",
            EvidenceIds: [phoneNumberId],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "validated",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent WhatsAppConfigurationRemoved(
        TenantScope scope, string userId, string? phoneNumberId, string auditReason, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            "integration.whatsapp.configuration_removed",
            DateTimeOffset.UtcNow,
            BeforeStatus: "Connected",
            AfterStatus: "Disconnected",
            EvidenceIds: phoneNumberId is null ? Array.Empty<string>() : [phoneNumberId],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    /// <summary>Generalizes WhatsAppConfigurationSaved to every IntegrationConnection type,
    /// connecting a tenant-configured provider (SMS, email, ...) is a governed action.</summary>
    public static AuditEvent ConnectionSaved(
        TenantScope scope, string userId, string connectionType, string providerKey, string auditReason, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            $"integration.connection.{connectionType.ToLowerInvariant()}.saved",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: "Connected",
            EvidenceIds: [providerKey],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "validated",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent ConnectionRemoved(
        TenantScope scope, string userId, string connectionType, string? providerKey, string auditReason, string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            $"integration.connection.{connectionType.ToLowerInvariant()}.removed",
            DateTimeOffset.UtcNow,
            BeforeStatus: "Connected",
            AfterStatus: "Disconnected",
            EvidenceIds: providerKey is null ? Array.Empty<string>() : [providerKey],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent AssistantPolicyQuestionAnswered(AgentActionLog actionLog) =>
        new(
            Guid.NewGuid(),
            actionLog.Scope,
            actionLog.CaseId,
            DocumentId: null,
            UserId: actionLog.UserId,
            ActorType.Agent,
            "assistant.policy_question.answered",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: actionLog.RiskFlag,
            EvidenceIds: actionLog.RetrievedSources.Select(source => source.ChunkId).ToArray(),
            ModelProvider: actionLog.ModelProvider,
            Confidence: actionLog.ConfidenceScore.Value,
            ValidationResult: actionLog.RiskFlag,
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: actionLog.CorrelationId);

    public static AuditEvent CommandCenterAssistantAnswered(AgentActionLog actionLog) =>
        new(
            Guid.NewGuid(),
            actionLog.Scope,
            actionLog.CaseId,
            DocumentId: null,
            UserId: actionLog.UserId,
            ActorType.Agent,
            "assistant.command_center.answered",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: actionLog.RiskFlag,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: actionLog.ModelProvider,
            Confidence: actionLog.ConfidenceScore.Value,
            ValidationResult: actionLog.RiskFlag,
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: actionLog.CorrelationId);

    public static AuditEvent CaseReviewGenerated(InsuranceIntelligence.Domain.CaseReview.CaseReviewSummary review, string? userId) =>
        new(
            Guid.NewGuid(),
            review.Scope,
            review.CaseId,
            DocumentId: null,
            UserId: userId,
            ActorType.Agent,
            "case_review.generated",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: review.RiskSignals.Count > 0 ? "signals_flagged" : "none",
            EvidenceIds: review.EvidenceRefs,
            ModelProvider: review.GeneratedByProvider,
            Confidence: review.Confidence,
            ValidationResult: review.RiskSignals.Count > 0 ? "flagged" : "clear",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: review.CorrelationId);

    public static AuditEvent WorkflowEvaluated(ClaimCaseSnapshot snapshot, string userId, string correlationId) =>
        ForCase(snapshot, userId, ActorType.System, "workflow.evaluated", null, snapshot.Status, "evaluated", correlationId);

    public static AuditEvent WorkflowTaskCreated(WorkflowTask task) =>
        ForWorkflowTask(task, "workflow.task.created", null, task.Status.ToString(), "created", task.CreatedByUserId);

    public static AuditEvent WorkflowTaskAssigned(WorkflowTask task, string assignedByUserId, string? beforeStatus) =>
        ForWorkflowTask(task, "workflow.task.assigned", beforeStatus, task.Status.ToString(), "assigned", assignedByUserId);

    public static AuditEvent WorkflowTaskCompleted(WorkflowTask task, string completedByUserId, string? beforeStatus) =>
        ForWorkflowTask(task, "workflow.task.completed", beforeStatus, task.Status.ToString(), "completed", completedByUserId);

    public static AuditEvent ApprovalCreated(ApprovalRequest approval) =>
        ForApproval(approval, approval.RequestedByUserId, "approval.created", null, approval.Status.ToString(), "created");

    public static AuditEvent ApprovalApproved(ApprovalRequest approval, string reviewerUserId, string? auditReason = null) =>
        ForApproval(approval, reviewerUserId, "approval.approved", ApprovalStatus.Pending.ToString(), approval.Status.ToString(), "approved", auditReason);

    public static AuditEvent ApprovalRejected(ApprovalRequest approval, string reviewerUserId, string? auditReason = null) =>
        ForApproval(approval, reviewerUserId, "approval.rejected", ApprovalStatus.Pending.ToString(), approval.Status.ToString(), "rejected", auditReason);

    public static AuditEvent ApprovalOverridden(ApprovalRequest approval, string reviewerUserId, string? auditReason = null) =>
        ForApproval(approval, reviewerUserId, "approval.overridden", ApprovalStatus.Pending.ToString(), approval.Status.ToString(), "overridden", auditReason);

    public static AuditEvent ApprovalEscalated(ApprovalRequest approval, string reviewerUserId, string? auditReason = null) =>
        ForApproval(approval, reviewerUserId, "approval.escalated", ApprovalStatus.Pending.ToString(), approval.Status.ToString(), "escalated", auditReason);

    public static AuditEvent ApprovalNextStepExecuted(ApprovalRequest approval, string executorUserId, string? auditReason = null) =>
        ForApproval(approval, executorUserId, "approval.next_step.executed", approval.Status.ToString(), approval.ExecutionResult, "executed", auditReason);

    public static AuditEvent CustomerUpdateDrafted(CustomerUpdateDraft update) =>
        ForCustomerUpdate(update, update.CreatedByUserId, ActorType.Agent, "customer_update.drafted", null, update.Status.ToString(), "drafted");

    public static AuditEvent CustomerUpdateApproved(CustomerUpdateDraft update, string approvedByUserId, string? auditReason = null) =>
        ForCustomerUpdate(update, approvedByUserId, ActorType.User, "customer_update.approved", CustomerUpdateStatus.PendingApproval.ToString(), update.Status.ToString(), "approved", auditReason: auditReason);

    public static AuditEvent CustomerUpdateRejected(CustomerUpdateDraft update, string rejectedByUserId, string? auditReason = null) =>
        ForCustomerUpdate(update, rejectedByUserId, ActorType.User, "customer_update.rejected", null, update.Status.ToString(), "rejected", auditReason: auditReason);

    public static AuditEvent CustomerUpdateEdited(CustomerUpdateDraft update, string editedByUserId, bool approvedInSameStep, string? auditReason = null) =>
        ForCustomerUpdate(
            update,
            editedByUserId,
            ActorType.User,
            approvedInSameStep ? "customer_update.edited_and_approved" : "customer_update.edited",
            null,
            update.Status.ToString(),
            "edited",
            auditReason: auditReason);

    public static AuditEvent CustomerUpdateWritten(CustomerUpdateDraft update) =>
        ForCustomerUpdate(update, update.CreatedByUserId, ActorType.User, "customer_update.written", null, update.Status.ToString(), "written");

    public static AuditEvent CustomerUpdateCreatedFromTemplate(CustomerUpdateDraft update) =>
        ForCustomerUpdate(update, update.CreatedByUserId, ActorType.User, "customer_update.from_template", null, update.Status.ToString(), "created", modelProvider: update.SourceTemplateKey ?? "Template");

    public static AuditEvent CustomerUpdateSent(CustomerUpdateDraft update, string sentByUserId, string? auditReason = null) =>
        ForCustomerUpdate(
            update,
            sentByUserId,
            ActorType.Integration,
            "customer_update.sent",
            null,
            update.Status.ToString(),
            update.Status == CustomerUpdateStatus.Sent ? "sent" : "simulated_sent",
            update.Status == CustomerUpdateStatus.Sent ? "GraphEmail" : "Mock",
            auditReason);

    public static AuditEvent EvidencePackExported(
        TenantScope scope,
        Guid caseId,
        string exportedByUserId,
        string objectKey,
        long fileSizeBytes,
        string auditReason,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            caseId,
            DocumentId: null,
            exportedByUserId,
            ActorType.User,
            "evidence_pack.exported",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: "exported",
            EvidenceIds: [objectKey],
            ModelProvider: "EvidencePackStorage",
            Confidence: fileSizeBytes,
            ValidationResult: "exported",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    /// <summary>Every product version save, whether a content edit or a status transition
    /// (submitted/approved/rejected/retired), is an append-only audited event, never a silent
    /// overwrite (outcomes A and C).</summary>
    public static AuditEvent ProductVersionSaved(
        TenantScope scope,
        string userId,
        Guid productId,
        int version,
        string status,
        string actionName,
        string auditReason,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: productId.ToString(),
            userId,
            ActorType.User,
            actionName,
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: status,
            EvidenceIds: [productId.ToString(), $"v{version}"],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: status,
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent CommunicationTemplateSaved(
        TenantScope scope,
        string userId,
        string templateKey,
        int version,
        bool active,
        string auditReason,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: templateKey,
            userId,
            ActorType.User,
            "communication_template.saved",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: active ? "Active" : "Inactive",
            EvidenceIds: [templateKey, $"v{version}"],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    public static AuditEvent DocumentTypeDefinitionSaved(
        TenantScope scope,
        string userId,
        string key,
        int version,
        bool active,
        string auditReason,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: key,
            userId,
            ActorType.User,
            "document_type_definition.saved",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: active ? "Active" : "Inactive",
            EvidenceIds: [key, $"v{version}"],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    private static AuditEvent ForCase(
        ClaimCaseSnapshot snapshot,
        string? userId,
        ActorType actorType,
        string actionName,
        string? beforeStatus,
        string? afterStatus,
        string validationResult,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            snapshot.Scope,
            snapshot.CaseId,
            DocumentId: null,
            userId,
            actorType,
            actionName,
            DateTimeOffset.UtcNow,
            beforeStatus,
            afterStatus,
            EvidenceIds: Array.Empty<string>(),
            ModelProvider: "Mock",
            Confidence: null,
            validationResult,
            ApprovalId: null,
            AuditReason: null,
            correlationId);

    private static AuditEvent ForWorkflowTask(
        WorkflowTask task,
        string actionName,
        string? beforeStatus,
        string? afterStatus,
        string validationResult,
        string? userId) =>
        new(
            Guid.NewGuid(),
            task.Scope,
            task.CaseId,
            DocumentId: null,
            userId,
            ActorType.User,
            actionName,
            DateTimeOffset.UtcNow,
            beforeStatus,
            afterStatus,
            EvidenceIds: [task.TaskId.ToString()],
            ModelProvider: "Mock",
            Confidence: null,
            validationResult,
            ApprovalId: null,
            AuditReason: null,
            task.CorrelationId);

    private static AuditEvent ForApproval(
        ApprovalRequest approval,
        string? userId,
        string actionName,
        string? beforeStatus,
        string? afterStatus,
        string validationResult,
        string? auditReason = null) =>
        new(
            Guid.NewGuid(),
            approval.Scope,
            approval.CaseId,
            DocumentId: null,
            userId,
            ActorType.User,
            actionName,
            DateTimeOffset.UtcNow,
            beforeStatus,
            afterStatus,
            EvidenceIds: approval.SourceReferences,
            ModelProvider: "Mock",
            Confidence: null,
            validationResult,
            ApprovalId: approval.ApprovalId.ToString(),
            AuditReason: auditReason,
            approval.CorrelationId);

    private static AuditEvent ForCustomerUpdate(
        CustomerUpdateDraft update,
        string? userId,
        ActorType actorType,
        string actionName,
        string? beforeStatus,
        string? afterStatus,
        string validationResult,
        string modelProvider = "Mock",
        string? auditReason = null) =>
        new(
            Guid.NewGuid(),
            update.Scope,
            update.CaseId,
            DocumentId: update.UpdateId.ToString(),
            userId,
            actorType,
            actionName,
            DateTimeOffset.UtcNow,
            beforeStatus,
            afterStatus,
            EvidenceIds: [update.UpdateId.ToString()],
            ModelProvider: modelProvider,
            Confidence: null,
            validationResult,
            ApprovalId: null,
            AuditReason: auditReason,
            update.CorrelationId);

    private static AuditEvent ForDocument(
        CaseDocument document,
        string? userId,
        ActorType actorType,
        string actionName,
        string? BeforeStatus,
        string? AfterStatus,
        string ValidationResult,
        string correlationId,
        decimal? confidence = null,
        string? auditReason = null) =>
        new(
            Guid.NewGuid(),
            document.Scope,
            document.CaseId,
            document.DocumentId.ToString(),
            userId,
            actorType,
            actionName,
            DateTimeOffset.UtcNow,
            BeforeStatus,
            AfterStatus,
            EvidenceIds: [document.ObjectKey],
            ModelProvider: "Mock",
            Confidence: confidence,
            ValidationResult,
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);

    private static AuditEvent ForKnowledge(
        KnowledgeDocument document,
        string? userId,
        ActorType actorType,
        string actionName,
        string? BeforeStatus,
        string? AfterStatus,
        string ValidationResult,
        string correlationId,
        string? auditReason = null) =>
        new(
            Guid.NewGuid(),
            document.Scope,
            CaseId: null,
            document.KnowledgeDocumentId.ToString(),
            userId,
            actorType,
            actionName,
            DateTimeOffset.UtcNow,
            BeforeStatus,
            AfterStatus,
            EvidenceIds: [document.ObjectKey],
            ModelProvider: "KnowledgeLifecycle",
            Confidence: null,
            ValidationResult,
            ApprovalId: null,
            AuditReason: auditReason,
            CorrelationId: correlationId);
}
