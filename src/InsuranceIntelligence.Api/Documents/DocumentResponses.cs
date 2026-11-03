using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Documents;

public sealed record DocumentUploadResponse(
    IReadOnlyCollection<DocumentResponse> Documents,
    DocumentChecklistResponse Checklist,
    SafeAuditSummary Audit,
    string? ProcessingCapability = null);

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
    IReadOnlyCollection<ActionStateResponse> BlockedActions,
    // The customer's own choice of what this document is, when they had one to make (a portal
    // upload against a named slot, or a numbered WhatsApp reply); null for a document nobody was
    // asked to name. DocumentType is always the classifier's independent guess, so a reader
    // compares the two itself for the disagreement marker (claim intake build prompt outcome B).
    string? CustomerDocumentTypeKey,
    bool IsSuperseded,
    string? ProcessingCapability = null);

public sealed record BoundingBoxResponse(
    int PageNumber,
    double X,
    double Y,
    double Width,
    double Height);

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
    string? CorrectionReference,
    BoundingBoxResponse? BoundingBox = null);

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
    IReadOnlyCollection<ChecklistDocumentResponse> MissingRequiredDocuments,
    ChecklistDetailsResponse? Details = null);

/// <summary>The details the received documents carry, apart from the documents themselves. Documents nothing
/// could be read from are listed separately: a person has to read them, they are not "missing everything".</summary>
public sealed record ChecklistDetailsResponse(
    int Found,
    int Total,
    decimal Percentage,
    IReadOnlyCollection<ChecklistMissingDetail> Missing,
    IReadOnlyCollection<string> UnreadDocuments);

public sealed record ChecklistMissingDetail(string DocumentLabel, string Detail);

public sealed record ChecklistDocumentResponse(
    string DocumentType,
    string Label,
    bool Received);

public sealed record ReclassifyDocumentRequest(
    string? DocumentType,
    string? UpdatedByUserId);

public sealed record ChecklistDocumentPreview(string DocumentType, string Label);

public sealed record ChecklistPreviewResponse(
    IReadOnlyCollection<ChecklistDocumentPreview> RequiredDocuments,
    IReadOnlyCollection<ChecklistDocumentPreview> OptionalDocuments);

public sealed record ApproveExtractionRequest(string? ApprovedByUserId);

public sealed record CorrectExtractionFieldRequest(
    string? FieldName,
    string? CorrectedValue);
