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
    string? ProcessingCapability = null,
    // True when the customer filed this under one type and the classifier confidently reads a
    // different one. A flag for staff only; never shown to the customer as an error.
    bool ClassifierDisagrees = false,
    // Build-loop item 11: the SHA-256 of the original exactly as received, what kind of slot it was
    // filed in ("Evidence" is kept as taken), and the address of a separate preview picture when one
    // was made. The original is never replaced by the preview.
    string? Sha256 = null,
    string? SlotKind = null,
    string? ImagePreviewUrl = null);

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
    ChecklistDetailsResponse? Details = null,
    IReadOnlyCollection<ChecklistSlotResponse>? Slots = null);

/// <summary>The details the received documents carry, apart from the documents themselves. Documents nothing
/// could be read from are listed separately: a person has to read them, they are not "missing everything".</summary>
public sealed record ChecklistDetailsResponse(
    int Found,
    int Total,
    decimal Percentage,
    IReadOnlyCollection<ChecklistMissingDetail> Missing,
    IReadOnlyCollection<string> UnreadDocuments);

public sealed record ChecklistMissingDetail(string DocumentLabel, string Detail);
/// <summary>One slot of the claim's checklist: its wording, kind (Document or Evidence), limits and
/// whether something has been received for it (build-loop item 4).</summary>
public sealed record ChecklistSlotResponse(
    string Key,
    string Label,
    string Description,
    bool Required,
    string Kind,
    bool AllowsMultipleFiles,
    int MaxFiles,
    IReadOnlyList<string> AcceptedFormats,
    bool Received);

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
    IReadOnlyCollection<ChecklistDocumentPreview> OptionalDocuments,
    IReadOnlyCollection<ChecklistSlotResponse>? Slots = null,
    string? ClaimTypeKey = null);

public sealed record ApproveExtractionRequest(string? ApprovedByUserId);

public sealed record CorrectExtractionFieldRequest(
    string? FieldName,
    string? CorrectedValue);
