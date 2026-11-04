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

    public static DocumentResponse ToResponse(CaseDocument document, decimal confidenceThreshold = 0.8m) =>
        new(
            document.DocumentId,
            document.CaseId,
            document.OriginalFileName,
            document.ContentType,
            document.FileSizeBytes,
            document.DocumentType.ToString(),
            document.ProcessingStatus.ToString(),
            document.Classification.ConfidenceScore,
            ToFieldResponses(document, confidenceThreshold),
            document.ValidationWarnings
                .Select(ToWarningResponse)
                .ToArray(),
            document.Extraction.Approved,
            document.UploadedAt,
            document.ProcessedAt,
            document.CorrelationId,
            $"/api/v1/cases/{document.CaseId}/documents/{document.DocumentId}/content",
            Allowed(document),
            Blocked(document),
            document.DocumentTypeKey,
            document.IsSuperseded,
            MissingProcessingCapability(document),
            document.DisagreesWithClassifier,
            document.Sha256,
            document.SlotKind?.ToString(),
            document.PreviewObjectKey is null ? null : $"/api/v1/cases/{document.CaseId}/documents/{document.DocumentId}/image-preview");

    private static string? MissingProcessingCapability(CaseDocument document) =>
        document.ProcessingStatus == DocumentProcessingStatus.Uploaded
        && string.Equals(document.Extraction.ProviderName, "NotConfigured", StringComparison.OrdinalIgnoreCase)
            ? "DocumentProcessing"
            : null;
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
            checklist.MissingRequiredDocuments.Select(missing => new ChecklistDocumentResponse(missing.Key, missing.Label, Received: false)).ToArray(),
            ToDetails(checklist),
            ToSlotResponses(checklist));

    private static ChecklistDetailsResponse ToDetails(DocumentCompletenessChecklist checklist)
    {
        var fields = checklist.FieldCompleteness;
        return new ChecklistDetailsResponse(
            fields.Found,
            fields.Total,
            fields.Percentage,
            checklist.DetailChecks
                .Where(check => check.Readable)
                .SelectMany(check => check.Missing.Select(field => new ChecklistMissingDetail(check.DocumentLabel, field.Label)))
                .ToArray(),
            checklist.UnreadDocuments.Select(check => check.DocumentLabel).ToArray());
    }

    private static IReadOnlyCollection<ChecklistSlotResponse> ToSlotResponses(DocumentCompletenessChecklist checklist)
    {
        var receivedKeys = checklist.ReceivedDocuments.Select(type => type.ToString())
            .Concat(checklist.CustomReceived)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return checklist.Slots
            .Select(slot => new ChecklistSlotResponse(
                slot.Key, slot.Label, slot.Description, slot.Required, slot.Kind.ToString(),
                slot.AllowsMultipleFiles, slot.MaxFiles, slot.AcceptedFormats, receivedKeys.Contains(slot.Key)))
            .ToArray();
    }

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

        if (ReprocessBlockedReason(document) is null)
        {
            actions.Add(Action("reprocessDocument", "Retry processing", Permission.UploadDocument));
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

        var reprocessReason = ReprocessBlockedReason(document);
        if (reprocessReason is not null)
        {
            actions.Add(Action("reprocessDocument", "Retry processing", Permission.UploadDocument, reprocessReason));
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

    /// <summary>Only a document that failed or is stuck before processing can be retried; a
    /// processed document is final until reclassified, and an approved extraction is locked.</summary>
    public static string? ReprocessBlockedReason(CaseDocument document) =>
        document.ProcessingStatus switch
        {
            DocumentProcessingStatus.Failed or DocumentProcessingStatus.Uploaded => null,
            DocumentProcessingStatus.ExtractionApproved => "Approved extractions are locked and cannot be reprocessed.",
            _ => "Only a failed or unprocessed document can be retried."
        };

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

    private static IReadOnlyCollection<DocumentFieldVerificationResponse> ToFieldResponses(CaseDocument document, decimal confidenceThreshold)
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

                BoundingBoxResponse? boundingBox = null;
                if (document.Extraction.FieldBoundingBoxes is not null
                    && document.Extraction.FieldBoundingBoxes.TryGetValue(field.Key, out var bb))
                {
                    boundingBox = new BoundingBoxResponse(bb.PageNumber, bb.X, bb.Y, bb.Width, bb.Height);
                }

                return new DocumentFieldVerificationResponse(
                    field.Key,
                    field.Value,
                    correction?.CorrectedValue ?? field.Value,
                    correction?.CorrectedValue,
                    confidence,
                    VerificationStatus(document, correction, confidence, warnings, confidenceThreshold),
                    Sensitivity(field.Key),
                    warnings,
                    correction?.CorrectedAt,
                    correction?.CorrelationId,
                    boundingBox);
            })
            .ToArray();
    }

    private static string VerificationStatus(
        CaseDocument document,
        DocumentFieldCorrection? correction,
        decimal? confidence,
        IReadOnlyCollection<DocumentValidationWarningResponse> warnings,
        decimal confidenceThreshold)
    {
        if (correction is not null)
        {
            return "Corrected";
        }

        if (document.Extraction.Approved)
        {
            return "Approved";
        }

        return confidence < confidenceThreshold || warnings.Count > 0
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
    public static IReadOnlyCollection<string> AllowedActions(CaseDocument document, decimal confidenceThreshold = 0.8m)
    {
        var actions = new List<string> { "RequestReplacement", "Escalate" };
        if (!document.Extraction.Approved)
        {
            actions.Add("VerifyFields");
        }

        if (!document.Extraction.Approved && document.Classification.ConfidenceScore >= confidenceThreshold && document.ValidationWarnings.Count == 0)
        {
            actions.Add("ApproveExtraction");
        }

        return actions;
    }

    public static IReadOnlyCollection<BlockedActionResponse> BlockedActions(CaseDocument document, decimal confidenceThreshold = 0.8m)
    {
        if (document.Extraction.Approved || (document.Classification.ConfidenceScore >= confidenceThreshold && document.ValidationWarnings.Count == 0))
        {
            return [];
        }

        return
        [
            new BlockedActionResponse(
                "ApproveExtraction",
                "HUMAN_VERIFICATION_REQUIRED",
                LowConfidenceReason(document, confidenceThreshold))
        ];
    }

    /// <summary>Outcome K: "a low confidence document lands in a person's queue with the reason
    /// shown" -- names the actual number and threshold rather than a generic sentence, so the
    /// reason is genuinely useful, not just present.</summary>
    public static string LowConfidenceReason(CaseDocument document, decimal confidenceThreshold = 0.8m)
    {
        var reasons = new List<string>();
        if (document.Classification.ConfidenceScore < confidenceThreshold)
        {
            reasons.Add($"classified as {document.DocumentType} at {document.Classification.ConfidenceScore:P0} confidence, below the {confidenceThreshold:P0} review threshold");
        }

        if (document.ValidationWarnings.Count > 0)
        {
            reasons.Add(document.ValidationWarnings.Count == 1
                ? $"1 validation warning ({document.ValidationWarnings.First().Message})"
                : $"{document.ValidationWarnings.Count} validation warnings");
        }

        return reasons.Count == 0
            ? "Human verification required before approval."
            : $"Needs review: {string.Join(", ", reasons)}.";
    }
}
