using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Documents;

public static class DocumentMapper
{
    private static readonly string[] InternalFieldTokens =
    [
        "objectkey",
        "storagekey",
        "blobpath",
        "connectionstring",
        "accesstoken",
        "refreshtoken",
        "clientsecret",
        "sastoken",
        "sasurl"
    ];

    private static readonly string[] SensitiveFieldTokens =
    [
        "name",
        "email",
        "phone",
        "address",
        "policy",
        "claimid",
        "caseid",
        "nationalid",
        "passport",
        "dateofbirth",
        "dob",
        "medical",
        "diagnosis",
        "account",
        "bank",
        "card"
    ];

    public static DocumentResponse ToResponse(CaseDocument document) =>
        new(
            document.DocumentId,
            document.CaseId,
            document.OriginalFileName,
            document.ContentType,
            document.FileSizeBytes,
            document.DocumentType.ToString(),
            document.ProcessingStatus.ToString(),
            document.Classification.ConfidenceScore,
            ToFieldResponses(document),
            document.ValidationWarnings
                .Select(ToWarningResponse)
                .ToArray(),
            document.Extraction.Approved,
            document.UploadedAt,
            document.ProcessedAt,
            document.CorrelationId,
            $"/api/cases/{document.CaseId}/documents/{document.DocumentId}/content",
            Allowed(document),
            Blocked(document));

    public static DocumentChecklistResponse ToResponse(DocumentCompletenessChecklist checklist) =>
        new(
            checklist.CaseId,
            checklist.ProductLine,
            checklist.CompletenessScore.Percentage,
            checklist.CompletenessScore.RequiredReceived,
            checklist.CompletenessScore.RequiredTotal,
            checklist.RequiredDocuments.Select(documentType => ToChecklistItem(documentType, checklist.ReceivedDocuments.Contains(documentType))).ToArray(),
            checklist.OptionalDocuments.Select(documentType => ToChecklistItem(documentType, checklist.ReceivedDocuments.Contains(documentType))).ToArray(),
            checklist.ReceivedDocuments.Select(documentType => ToChecklistItem(documentType, Received: true)).ToArray(),
            checklist.MissingRequiredDocuments.Select(missing => new ChecklistDocumentResponse(missing.DocumentType.ToString(), missing.Label, Received: false)).ToArray());

    public static SafeAuditSummary ToAuditSummary(IReadOnlyCollection<AuditEvent> auditEvents)
    {
        var lastEvent = auditEvents.OrderByDescending(auditEvent => auditEvent.Timestamp).FirstOrDefault();

        return new SafeAuditSummary(
            auditEvents.Count,
            lastEvent?.ActionName,
            lastEvent?.Timestamp,
            lastEvent?.CorrelationId);
    }

    public static string LabelFor(DocumentType documentType) =>
        documentType switch
        {
            DocumentType.ClaimForm => "Claim form",
            DocumentType.PoliceAbstract => "Police abstract",
            DocumentType.Invoice => "Invoice",
            DocumentType.MedicalReport => "Medical report",
            DocumentType.PhotoEvidence => "Photo evidence",
            DocumentType.CustomerId => "Customer ID",
            DocumentType.GarageEstimate => "Garage estimate",
            _ => "Unknown document"
        };

    public static IReadOnlyCollection<ActionStateResponse> Allowed(CaseDocument document)
    {
        var actions = new List<ActionStateResponse>();

        if (ReclassifyBlockedReason(document) is null)
        {
            actions.Add(Action("reclassifyDocument", "Reclassify document", Permission.UploadDocument));
        }

        if (ApproveExtractionBlockedReason(document) is null)
        {
            actions.Add(Action("approveDocumentExtraction", "Approve extraction", Permission.UploadDocument));
        }

        if (CorrectExtractionBlockedReason(document) is null)
        {
            actions.Add(Action("correctDocumentExtraction", "Correct extracted field", Permission.UploadDocument));
        }

        return actions;
    }

    public static IReadOnlyCollection<ActionStateResponse> Blocked(CaseDocument document)
    {
        var actions = new List<ActionStateResponse>();
        var reclassifyReason = ReclassifyBlockedReason(document);
        if (reclassifyReason is not null)
        {
            actions.Add(Action("reclassifyDocument", "Reclassify document", Permission.UploadDocument, reclassifyReason));
        }

        var approveReason = ApproveExtractionBlockedReason(document);
        if (approveReason is not null)
        {
            actions.Add(Action("approveDocumentExtraction", "Approve extraction", Permission.UploadDocument, approveReason));
        }

        var correctReason = CorrectExtractionBlockedReason(document);
        if (correctReason is not null)
        {
            actions.Add(Action("correctDocumentExtraction", "Correct extracted field", Permission.UploadDocument, correctReason));
        }

        return actions;
    }

    public static string? ReclassifyBlockedReason(CaseDocument document) =>
        document.ProcessingStatus switch
        {
            DocumentProcessingStatus.Processing => "Document processing must finish before reclassification.",
            DocumentProcessingStatus.Failed => "Failed documents must be reprocessed before reclassification.",
            DocumentProcessingStatus.ExtractionApproved => "Approved extractions are locked and cannot be reclassified.",
            _ when document.Extraction.Approved => "Approved extractions are locked and cannot be reclassified.",
            _ => null
        };

    public static string? ApproveExtractionBlockedReason(CaseDocument document)
    {
        if (document.ProcessingStatus == DocumentProcessingStatus.ExtractionApproved || document.Extraction.Approved)
        {
            return "This extraction has already been approved.";
        }

        if (document.ProcessingStatus is DocumentProcessingStatus.Uploaded or DocumentProcessingStatus.Processing)
        {
            return "Only processed documents can have extraction approved.";
        }

        if (document.ProcessingStatus == DocumentProcessingStatus.Failed)
        {
            return "Failed documents must be reprocessed before extraction approval.";
        }

        if (document.DocumentType == DocumentType.Unknown)
        {
            return "Reclassify this document before approving extraction.";
        }

        return document.ValidationWarnings.Any(IsBlockingWarning)
            ? "Resolve blocking validation warnings before approving extraction."
            : null;
    }

    public static string? CorrectExtractionBlockedReason(CaseDocument document, string? fieldName = null)
    {
        if (document.ProcessingStatus == DocumentProcessingStatus.ExtractionApproved || document.Extraction.Approved)
        {
            return "Approved extractions are locked and cannot be corrected.";
        }

        if (document.ProcessingStatus != DocumentProcessingStatus.Processed)
        {
            return "Only processed documents can have extracted fields corrected.";
        }

        var exposedFields = document.Extraction.Fields.Keys.Where(IsFieldExposed).ToArray();
        if (exposedFields.Length == 0)
        {
            return "This document has no extracted fields available for correction.";
        }

        if (!string.IsNullOrWhiteSpace(fieldName)
            && !exposedFields.Any(candidate => candidate.Equals(fieldName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return "The requested extracted field is not available for correction.";
        }

        return null;
    }

    public static bool IsFieldExposed(string fieldName)
    {
        var normalized = NormalizeFieldName(fieldName);
        return normalized.Length > 0
            && !InternalFieldTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }

    private static bool IsBlockingWarning(DocumentValidationWarning warning) =>
        warning.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)
        || warning.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyCollection<DocumentFieldVerificationResponse> ToFieldResponses(CaseDocument document)
    {
        var corrections = document.Extraction.FieldCorrections
            ?? new Dictionary<string, DocumentFieldCorrection>();

        return document.Extraction.Fields
            .Where(field => IsFieldExposed(field.Key))
            .OrderBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
            .Select(field =>
            {
                var correction = corrections.Values.FirstOrDefault(candidate =>
                    candidate.FieldName.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
                var confidence = document.Extraction.FieldConfidences.TryGetValue(field.Key, out var fieldConfidence)
                    ? fieldConfidence
                    : (decimal?)null;
                var warnings = document.ValidationWarnings
                    .Where(warning => warning.FieldName?.Equals(field.Key, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(ToWarningResponse)
                    .ToArray();

                return new DocumentFieldVerificationResponse(
                    field.Key,
                    field.Value,
                    correction?.CorrectedValue ?? field.Value,
                    correction?.CorrectedValue,
                    confidence,
                    VerificationStatus(document, correction, confidence, warnings),
                    Sensitivity(field.Key),
                    warnings,
                    correction?.CorrectedAt,
                    correction?.CorrelationId);
            })
            .ToArray();
    }

    private static string VerificationStatus(
        CaseDocument document,
        DocumentFieldCorrection? correction,
        decimal? confidence,
        IReadOnlyCollection<DocumentValidationWarningResponse> warnings)
    {
        if (correction is not null)
        {
            return "Corrected";
        }

        if (document.Extraction.Approved)
        {
            return "Approved";
        }

        return confidence is < 0.8m || warnings.Count > 0
            ? "NeedsReview"
            : "Extracted";
    }

    private static string Sensitivity(string fieldName)
    {
        var normalized = NormalizeFieldName(fieldName);
        return SensitiveFieldTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal))
            ? "Sensitive"
            : "Standard";
    }

    private static string NormalizeFieldName(string fieldName) =>
        new(fieldName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static DocumentValidationWarningResponse ToWarningResponse(DocumentValidationWarning warning) =>
        new(
            warning.Code,
            warning.Message,
            warning.Severity,
            warning.FieldName,
            warning.ExpectedValue,
            warning.ObservedValue);

    private static ActionStateResponse Action(string action, string label, Permission permission, string? reason = null) =>
        new(action, label, permission.ToString(), reason);

    private static ChecklistDocumentResponse ToChecklistItem(DocumentType documentType, bool Received) =>
        new(documentType.ToString(), LabelFor(documentType), Received);
}

public static class DocumentActionState
{
    public static IReadOnlyCollection<string> AllowedActions(CaseDocument document)
    {
        var actions = new List<string> { "RequestReplacement", "Escalate" };
        if (!document.Extraction.Approved)
        {
            actions.Add("VerifyFields");
        }

        if (!document.Extraction.Approved && document.Classification.ConfidenceScore >= 0.8m && document.ValidationWarnings.Count == 0)
        {
            actions.Add("ApproveExtraction");
        }

        return actions;
    }

    public static IReadOnlyCollection<BlockedActionResponse> BlockedActions(CaseDocument document)
    {
        if (document.Extraction.Approved || (document.Classification.ConfidenceScore >= 0.8m && document.ValidationWarnings.Count == 0))
        {
            return [];
        }

        return
        [
            new BlockedActionResponse(
                "ApproveExtraction",
                "HUMAN_VERIFICATION_REQUIRED",
                "Low-confidence or warned extraction requires human verification before approval.")
        ];
    }
}
