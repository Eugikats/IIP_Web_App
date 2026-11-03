namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// What a slot holds (build-loop item 4). A <see cref="Document"/> is paperwork (a police letter,
/// a log book): it can be scanned, cleaned up and turned into a PDF. <see cref="Evidence"/> is
/// proof of what happened (photos of the damage): it is kept exactly as taken, never recompressed
/// or altered, and its metadata is preserved.
/// </summary>
public enum DocumentKind
{
    Document,
    Evidence,
}

/// <summary>
/// One slot a customer can put files into, resolved for a particular claim: the requirement as the
/// insurer configured it (or the built-in default), with the words the customer sees. This is the
/// single shape the customer page, the staff checklist and completeness all read.
/// </summary>
public sealed record ResolvedDocumentSlot(
    string Key,
    string Label,
    string Description,
    bool Required,
    DocumentKind Kind,
    bool AllowsMultiplePages,
    int MaxFiles,
    IReadOnlyList<string> AcceptedFormats,
    bool IsRegulatoryOrWordingMandated = false)
{
    /// <summary>True when more than one file may be stored in the slot (pages of one document, or
    /// several photos).</summary>
    public bool AllowsMultipleFiles => MaxFiles > 1;

    public bool Accepts(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return extension.Length > 0 && AcceptedFormats.Any(format => string.Equals(format, extension, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Sensible defaults for each kind, used when an insurer does not say otherwise and for
/// the built-in rules. A document is one file unless pages are allowed; evidence is several
/// photos.</summary>
public static class DocumentSlotDefaults
{
    public const int DefaultMultiPageLimit = 10;
    public const int DefaultEvidenceLimit = 10;
    public const int AbsoluteMaxFiles = 30;

    public static readonly IReadOnlyList<string> DocumentFormats = ["pdf", "jpg", "jpeg", "png", "heic", "heif", "tif", "tiff"];
    public static readonly IReadOnlyList<string> EvidenceFormats = ["jpg", "jpeg", "png", "heic", "heif"];

    /// <summary>Every file extension the platform will accept in any slot (kept in step with
    /// DocumentUploadValidation's supported types, minus plain text which is staff-only).</summary>
    public static readonly IReadOnlyList<string> AllSlotFormats = ["pdf", "jpg", "jpeg", "png", "heic", "heif", "tif", "tiff"];

    public static IReadOnlyList<string> FormatsFor(DocumentKind kind) =>
        kind == DocumentKind.Evidence ? EvidenceFormats : DocumentFormats;

    public static int MaxFilesFor(DocumentKind kind, bool allowsMultiplePages) =>
        kind == DocumentKind.Evidence ? DefaultEvidenceLimit : allowsMultiplePages ? DefaultMultiPageLimit : 1;

    /// <summary>The kind a built-in document type has when nothing says otherwise: photos of the
    /// damage are evidence, everything else is a document.</summary>
    public static DocumentKind KindFor(DocumentType type) =>
        type == DocumentType.PhotoEvidence ? DocumentKind.Evidence : DocumentKind.Document;

    public static ResolvedDocumentSlot ForBuiltIn(DocumentType type, string label, string description, bool required)
    {
        var kind = KindFor(type);
        var multiple = kind == DocumentKind.Evidence;
        return new ResolvedDocumentSlot(
            type.ToString(), label, description, required, kind, AllowsMultiplePages: multiple,
            MaxFilesFor(kind, multiple), FormatsFor(kind));
    }
}
