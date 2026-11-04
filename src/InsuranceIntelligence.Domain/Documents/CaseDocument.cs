using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public sealed record CaseDocument
{
    public required Guid DocumentId { get; init; }
    public required Guid CaseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string ObjectKey { get; init; }
    public required DocumentType DocumentType { get; init; }

    /// <summary>Which document this actually is, as a catalog key (claim intake build prompt
    /// outcome A). Set from the customer's own choice when they uploaded it against a named slot
    /// (outcome B); falls back to <see cref="DocumentType"/>'s name when the customer had no choice
    /// to make (email and older WhatsApp uploads) or on a document stored before this field
    /// existed. Never required, so old JSON payloads with no such property deserialize cleanly.
    /// The customer's choice is what completeness matches against; <see cref="Classification"/>
    /// stays the classifier's own independent guess, so the two can be compared.</summary>
    public string? DocumentTypeKey { get; init; }

    public required DocumentProcessingStatus ProcessingStatus { get; init; }
    public required DocumentClassificationResult Classification { get; init; }
    public required DocumentExtractionResult Extraction { get; init; }
    public required IReadOnlyCollection<DocumentValidationWarning> ValidationWarnings { get; init; }
    public required string UploadedByUserId { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public required string CorrelationId { get; init; }

    /// <summary>A customer "removing" or "replacing" a document never deletes it (non-negotiable
    /// 2, claim intake build prompt outcome B): it marks the earlier file superseded here, and the
    /// file and its history stay visible to staff. Completeness and every checklist ignore a
    /// superseded document as if it had not been sent. Never required, so a document stored before
    /// this field existed deserializes as not superseded.</summary>
    public bool IsSuperseded { get; init; }

    /// <summary>The page number the customer gave this file within a multi-page slot (build-loop
    /// item 12); null means "in the order sent". Only the order matters, the numbers are renumbered
    /// whenever pages are rearranged.</summary>
    public int? PageOrder { get; init; }

    /// <summary>What the recogniser made of this file (build-loop item 14): which of the workspace's
    /// document types it looks like, how sure, and why. Null before it has been looked at.</summary>
    public DocumentRecognition? Recognition { get; init; }

    /// <summary>The name this document is shown under, such as <c>PoliceReport_CLM-1A2B3C4D.jpg</c>.
    /// <see cref="OriginalFileName"/> is the file exactly as sent and is never changed.</summary>
    public string? DisplayName { get; init; }

    /// <summary>True when a person should decide where this belongs: the recogniser could not tell
    /// what it is, or what it recognised is not something the claim has a place for.</summary>
    public bool NeedsReview { get; init; }

    public string? ReviewReason { get; init; }

    /// <summary>True when the file arrived with no slot and was placed in one because the recogniser
    /// was confident. A person can still move it.</summary>
    public bool FiledByRecognition { get; init; }

    /// <summary>The type the file looks like, from the best evidence there is: the recogniser when it
    /// was confident, else the built-in classifier. Null when neither could tell.</summary>
    public string? RecognisedKey =>
        Recognition is { IsConfident: true } recognised
            ? recognised.TypeKey
            : Classification.DocumentType != Documents.DocumentType.Unknown ? Classification.DocumentType.ToString() : null;

    /// <summary>SHA-256 of the bytes exactly as received (build-loop item 11), as lower-case hex.
    /// Lets anyone show later that the stored original is the file that was sent. Null for a
    /// document stored before this field existed.</summary>
    public string? Sha256 { get; init; }

    /// <summary>What kind of slot this was filed in, when the customer filed it in one. Evidence is
    /// kept exactly as taken: never recompressed, resized or stripped.</summary>
    public DocumentKind? SlotKind { get; init; }

    /// <summary>Where the separate preview of a picture is stored (a JPEG made for looking at,
    /// with no location or camera details). Null when there is none (a PDF, an unreadable file).
    /// The original is never replaced by it.</summary>
    public string? PreviewObjectKey { get; init; }

    public DateTimeOffset? SupersededAt { get; init; }

    /// <summary>The catalog key this document is filed under: the customer's own choice when they
    /// uploaded into a named slot, otherwise the classifier's type. This is the one key that
    /// completeness, the customer page and the staff checklist all match on.</summary>
    public string EffectiveKey => DocumentTypeKey ?? DocumentType.ToString();

    /// <summary>True when this file stands in for a required slot. The customer did their part as
    /// soon as the file is stored in the slot, so it counts whether or not it has finished being
    /// read, but not once it is superseded, failed to process, or was rejected by a person.</summary>
    public bool CountsTowardsCompleteness =>
        !IsSuperseded
        && ProcessingStatus is not (DocumentProcessingStatus.Failed or DocumentProcessingStatus.Rejected)
        && !string.Equals(EffectiveKey, nameof(Documents.DocumentType.Unknown), StringComparison.OrdinalIgnoreCase);

    /// <summary>The customer filed this in one slot but the classifier confidently reads it as a
    /// different built-in type. Never shown to the customer as an error; a flag for staff.</summary>
    public bool DisagreesWithClassifier =>
        DocumentTypeKey is not null
        && RecognisedKey is not null
        && !string.Equals(DocumentTypeKey, RecognisedKey, StringComparison.OrdinalIgnoreCase);
}
