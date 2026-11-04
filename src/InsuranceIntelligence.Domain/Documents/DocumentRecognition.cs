namespace InsuranceIntelligence.Domain.Documents;

/// <summary>One document type the workspace recognises: the built-in ones and any the insurer added.
/// This is the list the recogniser chooses from.</summary>
public sealed record DocumentCatalogueEntry(string Key, string Label, string Description);

/// <summary>
/// What a recogniser made of an uploaded file (build-loop item 14): which catalogue type it looks
/// like, how sure it is, and why in a sentence a person can check. A null <see cref="TypeKey"/>
/// means it could not tell. It is advice: the customer's own choice of slot always stands, and a
/// person decides anything it is unsure about.
/// </summary>
public sealed record DocumentRecognition(
    string? TypeKey,
    decimal Confidence,
    string Reason,
    string ProviderName,
    string? ModelVersion,
    DateTimeOffset RecognisedAt)
{
    /// <summary>Sure enough to file a document that has no slot, or to flag a customer's choice.</summary>
    public const decimal ConfidentThreshold = 0.8m;

    public bool IsConfident => TypeKey is not null && Confidence >= ConfidentThreshold;
}

public sealed record DocumentRecognitionRequest(
    string FileName,
    string ContentType,
    Func<CancellationToken, Task<Stream>> OpenAsync,
    IReadOnlyList<DocumentCatalogueEntry> Catalogue);

/// <summary>Looks at an uploaded file and says which of the workspace's document types it is.
/// Implementations never throw for an unreadable or unrecognisable file: they return a recognition
/// with no type, so a person looks at it.</summary>
public interface IDocumentRecognizer
{
    Task<DocumentRecognition> RecognizeAsync(DocumentRecognitionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The name a stored document is shown under: the type's label with no spaces, the claim's
/// reference, a number when the claim already has one of that kind, and the original extension, for
/// example <c>PoliceReport_CLM-1A2B3C4D_2.jpg</c>. The file as sent keeps its own name.</summary>
public static class DocumentNaming
{
    public const string UnsortedLabel = "Unsorted";

    /// <summary>"Police report" becomes "PoliceReport"; anything that is not a letter or digit is
    /// dropped. Never empty.</summary>
    public static string Compact(string label)
    {
        var words = (label ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
            .Where(word => word.Length > 0)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..])
            .ToArray();
        var compact = string.Concat(words);
        return compact.Length == 0 ? UnsortedLabel : compact;
    }

    /// <param name="ordinal">1 for the first document of this kind on the claim (no number is shown),
    /// 2 for the second, and so on.</param>
    public static string DisplayName(string label, string claimReference, int ordinal, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        var number = ordinal > 1 ? $"_{ordinal}" : string.Empty;
        return $"{Compact(label)}_{claimReference}{number}{extension}";
    }
}
