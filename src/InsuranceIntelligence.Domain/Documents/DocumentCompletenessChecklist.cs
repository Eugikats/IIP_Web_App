namespace InsuranceIntelligence.Domain.Documents;

/// <summary>How many of the details the received documents must carry were found, counting only documents that could
/// be read. A document nothing could be read from is not counted either way.</summary>
public sealed record FieldCompleteness(int Found, int Total, decimal Percentage);

/// <summary>The Custom* fields (claim intake build prompt outcome A) cover a product's document
/// types that are not one of the eight built-in <see cref="DocumentType"/> values. They default to
/// empty so every existing construction (built before custom types existed) keeps compiling and
/// behaving exactly as before. A custom requirement that is missing is already folded into
/// <see cref="MissingRequiredDocuments"/>, so the existing readers of that list need no changes.
/// <see cref="FieldChecks"/> is the detail-level result for each received document; it too defaults to empty.</summary>
public sealed record DocumentCompletenessChecklist(
    Guid CaseId,
    string ProductLine,
    IReadOnlyCollection<DocumentType> RequiredDocuments,
    IReadOnlyCollection<DocumentType> OptionalDocuments,
    IReadOnlyCollection<DocumentType> ReceivedDocuments,
    IReadOnlyCollection<MissingDocumentRequirement> MissingRequiredDocuments,
    CompletenessScore CompletenessScore,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomRequiredDocuments = null,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomOptionalDocuments = null,
    IReadOnlyCollection<string>? CustomReceivedKeys = null,
    IReadOnlyCollection<DocumentFieldCheckResult>? FieldChecks = null,
    IReadOnlyList<ResolvedDocumentSlot>? ResolvedSlots = null)
{
    /// <summary>The slots a customer can fill for this claim, with their wording, kind and limits
    /// (build-loop item 4). Empty only for a checklist built by code that predates slots.</summary>
    public IReadOnlyList<ResolvedDocumentSlot> Slots => ResolvedSlots ?? [];

    public IReadOnlyCollection<CustomDocumentRequirement> CustomRequired => CustomRequiredDocuments ?? [];

    public IReadOnlyCollection<CustomDocumentRequirement> CustomOptional => CustomOptionalDocuments ?? [];

    public IReadOnlyCollection<string> CustomReceived => CustomReceivedKeys ?? [];

    public IReadOnlyCollection<DocumentFieldCheckResult> DetailChecks => FieldChecks ?? [];

    /// <summary>Documents whose text could not be read, so a person has to read them.</summary>
    public IReadOnlyCollection<DocumentFieldCheckResult> UnreadDocuments =>
        DetailChecks.Where(check => !check.Readable).ToArray();

    public FieldCompleteness FieldCompleteness
    {
        get
        {
            var readable = DetailChecks.Where(check => check.Readable).ToArray();
            var found = readable.Sum(check => check.Found.Count);
            var total = found + readable.Sum(check => check.Missing.Count);
            return new FieldCompleteness(found, total, total == 0 ? 100m : Math.Round((decimal)found / total * 100m, 2));
        }
    }
}
