using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// Where a human correction (build prompt outcome K, layer 3) is recorded as reusable labelled
/// data, not on the document alone: "every human correction, whether a re-label or a retyped
/// value, is stored with the document as labelled data ... and accumulates for the next
/// classifier version. Nothing is retrained silently. Training data never leaves the tenant."
///
/// One row per correction event. Written alongside, not instead of, the existing per-document
/// mutation (CaseDocument.Classification / Extraction.FieldCorrections) and the audit trail: the
/// audit trail answers "what happened to this document," this answers "what has this tenant's
/// classifier gotten wrong or right so far," which needs to survive independently of any single
/// document's own edit history to be usable as a training/accuracy signal.
/// </summary>
public enum DocumentLabelSource
{
    /// <summary>A person changed the document's classified type.</summary>
    Reclassification,

    /// <summary>A person retyped one extracted field's value.</summary>
    FieldCorrection,

    /// <summary>A person approved the extraction as-is: the AI's classification was correct.</summary>
    ConfirmedWithoutCorrection,
}

public sealed record DocumentLabelledExample(
    Guid ExampleId,
    TenantScope Scope,
    Guid DocumentId,
    Guid CaseId,
    DocumentType OriginalPredictedType,
    decimal OriginalConfidence,
    DocumentType ConfirmedType,
    IReadOnlyDictionary<string, string> ConfirmedFields,
    DocumentLabelSource Source,
    string RecordedByUserId,
    DateTimeOffset RecordedAt,
    string CorrelationId);

public interface IDocumentLabelledExampleRepository
{
    Task SaveAsync(DocumentLabelledExample example, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentLabelledExample>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
