using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>What a classifier decided and on what evidence, so a person can see why.</summary>
public sealed record DocumentClassificationGuess(DocumentType Type, decimal Confidence, string Basis);

/// <summary>
/// Decides what kind of document a file is. The words printed on the document come first, because they are what
/// the document actually is; a file name is only a weak hint, because a customer's phone names a photo
/// IMG-20261027-WA0001.jpg and a scan scan001.pdf. File-name words are matched as whole words, never as pieces of a
/// word ("accident" and "provider" both contain "id" and are not identity documents). A guess made only from the file
/// name carries a low confidence, which keeps it under the human-verification threshold. Pure and deterministic.
/// </summary>
public static class DocumentContentClassifier
{
    public const decimal ContentConfidence = 0.85m;
    public const decimal FileNameConfidence = 0.55m;
    public const decimal UnknownConfidence = 0.30m;

    // Title-style phrases only. Words that merely appear in the body of another document (a claim form asks for a
    // diagnosis, an ID number and a police report number) are deliberately left out. The phrase that appears
    // earliest in the text wins, because a document's title comes before the rest of what it says.
    private static readonly (DocumentType Type, string[] Phrases)[] ContentPhrases =
    [
        (DocumentType.PoliceAbstract, ["police abstract", "accident abstract", "abstract of accident", "occurrence book extract"]),
        (DocumentType.MedicalReport, ["medical report", "discharge summary", "medical certificate", "clinical summary"]),
        (DocumentType.GarageEstimate, ["repair estimate", "estimate of repair", "garage estimate", "repair quotation", "proforma invoice", "pro-forma invoice", "quotation for repair"]),
        (DocumentType.ClaimForm, ["claim form", "claim notification", "notification of claim", "motor accident claim", "accident claim", "motor claim", "windscreen glass claim"]),
        (DocumentType.CustomerId, ["national identity card", "national identification card", "national id card", "driving permit", "driving licence", "driving license"]),
        (DocumentType.Invoice, ["tax invoice", "invoice no", "invoice number", "receipt no", "receipt number", "official receipt"]),
    ];

    // Whole-word file name hints, checked on the words of the name.
    private static readonly (DocumentType Type, string[] Words)[] FileNameWords =
    [
        (DocumentType.PoliceAbstract, ["police", "abstract"]),
        (DocumentType.ClaimForm, ["claim", "claimform"]),
        (DocumentType.GarageEstimate, ["estimate", "quotation", "quote", "proforma"]),
        (DocumentType.Invoice, ["invoice", "receipt", "bill"]),
        (DocumentType.MedicalReport, ["medical", "discharge", "hospital", "doctor"]),
        (DocumentType.PhotoEvidence, ["photo", "photograph", "picture"]),
        (DocumentType.CustomerId, ["id", "nin", "licence", "license", "permit", "passport"]),
    ];

    public static DocumentClassificationGuess Classify(string fileName, string? content = null, string? providerDocumentType = null)
    {
        var provider = FromProviderType(providerDocumentType);
        if (provider is not DocumentType.Unknown)
        {
            return new DocumentClassificationGuess(provider, ContentConfidence, "the document type reported by the reading service");
        }

        if (!string.IsNullOrWhiteSpace(content))
        {
            var text = Normalise(content);
            var earliest = ContentPhrases
                .SelectMany(entry => entry.Phrases.Select(phrase => (entry.Type, Phrase: phrase, Index: text.IndexOf(phrase, StringComparison.Ordinal))))
                .Where(hit => hit.Index >= 0)
                .OrderBy(hit => hit.Index)
                .Cast<(DocumentType Type, string Phrase, int Index)?>()
                .FirstOrDefault();
            if (earliest is { } found)
            {
                return new DocumentClassificationGuess(found.Type, ContentConfidence, $"the words \"{found.Phrase}\" printed on the document");
            }
        }

        var words = Regex.Split(fileName.ToLowerInvariant(), "[^a-z0-9]+").Where(word => word.Length > 0).ToHashSet();
        foreach (var (type, hints) in FileNameWords)
        {
            var hit = hints.FirstOrDefault(words.Contains);
            if (hit is not null)
            {
                return new DocumentClassificationGuess(type, FileNameConfidence, $"the word \"{hit}\" in the file name only");
            }
        }

        return new DocumentClassificationGuess(DocumentType.Unknown, UnknownConfidence, "nothing recognisable in the text or the file name");
    }

    private static DocumentType FromProviderType(string? providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return DocumentType.Unknown;
        }

        var value = providerType.ToLowerInvariant();
        if (value.Contains("invoice") || value.Contains("receipt")) return DocumentType.Invoice;
        if (value.Contains("medical")) return DocumentType.MedicalReport;
        if (value.Contains("identity") || value.Contains("iddocument")) return DocumentType.CustomerId;
        return DocumentType.Unknown;
    }

    private static string Normalise(string content) =>
        Regex.Replace(content.ToLowerInvariant(), "[^a-z0-9\\-]+", " ").Trim();
}
