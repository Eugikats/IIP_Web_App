using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Documents;

public sealed record DocumentUploadResponse(
    IReadOnlyCollection<DocumentResponse> Documents,
    DocumentChecklistResponse Checklist,
    SafeAuditSummary Audit);

public sealed record DocumentResponse(
    Guid DocumentId,
    Guid CaseId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string DocumentType,
    string ProcessingStatus,
    decimal ConfidenceScore,
    IReadOnlyCollection<DocumentFieldVerificationResponse> Fields,
    IReadOnlyCollection<DocumentValidationWarningResponse> ValidationWarnings,
    bool ExtractionApproved,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessedAt,
    string CorrelationId,
    string PreviewUrl,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);

public sealed record DocumentFieldVerificationResponse(
    string Name,
    string ExtractedValue,
    string EffectiveValue,
    string? CorrectedValue,
    decimal? ConfidenceScore,
    string VerificationStatus,
    string Sensitivity,
    IReadOnlyCollection<DocumentValidationWarningResponse> Warnings,
    DateTimeOffset? CorrectedAt,
    string? CorrectionReference);

public sealed record DocumentValidationWarningResponse(
    string Code,
    string Message,
    string Severity,
    string? FieldName,
    string? ExpectedValue,
    string? ObservedValue);

public sealed record DocumentChecklistResponse(
    Guid CaseId,
    string ProductLine,
    decimal CompletenessScore,
    int RequiredReceived,
    int RequiredTotal,
    IReadOnlyCollection<ChecklistDocumentResponse> RequiredDocuments,
    IReadOnlyCollection<ChecklistDocumentResponse> OptionalDocuments,
    IReadOnlyCollection<ChecklistDocumentResponse> ReceivedDocuments,
    IReadOnlyCollection<ChecklistDocumentResponse> MissingRequiredDocuments);

public sealed record ChecklistDocumentResponse(
    string DocumentType,
    string Label,
    bool Received);

public sealed record ReclassifyDocumentRequest(
    string? DocumentType,
    string? UpdatedByUserId);

public sealed record ApproveExtractionRequest(string? ApprovedByUserId);

public sealed record CorrectExtractionFieldRequest(
    string? FieldName,
    string? CorrectedValue);
