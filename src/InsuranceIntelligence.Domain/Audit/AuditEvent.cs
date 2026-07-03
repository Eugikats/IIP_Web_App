using InsuranceIntelligence.Domain.Common;
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

    public static AuditEvent CustomerUpdateDrafted(CustomerUpdateDraft update) =>
        ForCustomerUpdate(update, update.CreatedByUserId, ActorType.Agent, "customer_update.drafted", null, update.Status.ToString(), "drafted");

    public static AuditEvent CustomerUpdateApproved(CustomerUpdateDraft update, string approvedByUserId, string? auditReason = null) =>
        ForCustomerUpdate(update, approvedByUserId, ActorType.User, "customer_update.approved", CustomerUpdateStatus.PendingApproval.ToString(), update.Status.ToString(), "approved", auditReason: auditReason);

    public static AuditEvent CustomerUpdateRejected(CustomerUpdateDraft update, string rejectedByUserId, string? auditReason = null) =>
        ForCustomerUpdate(update, rejectedByUserId, ActorType.User, "customer_update.rejected", null, update.Status.ToString(), "rejected", auditReason: auditReason);

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
