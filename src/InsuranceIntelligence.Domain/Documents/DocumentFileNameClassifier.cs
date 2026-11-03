namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// A last-resort guess at a document type from its file name. It matches whole tokens only: the
/// name is split on separators and each token is compared exactly. The earlier implementation used
/// substring matching, so "accident_report.pdf" (contains "id") was filed as a customer ID.
/// A name that carries no recognised token is Unknown, which is the honest answer: for example a
/// phone camera name like IMG_2031.jpg says nothing about what the photo shows.
/// </summary>
public static class DocumentFileNameClassifier
{
    private static readonly char[] Separators = ['_', '-', ' ', '.', '(', ')', '[', ']', ','];

    public static DocumentType Classify(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return DocumentType.Unknown;
        }

        var tokens = Path.GetFileNameWithoutExtension(fileName)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant())
            .ToHashSet();

        if (tokens.Contains("claim") && tokens.Contains("form")) return DocumentType.ClaimForm;
        if (tokens.Contains("claimform")) return DocumentType.ClaimForm;
        if (tokens.Contains("police") || tokens.Contains("abstract")) return DocumentType.PoliceAbstract;
        if (tokens.Contains("invoice") || tokens.Contains("receipt")) return DocumentType.Invoice;
        if (tokens.Contains("medical") || tokens.Contains("discharge")) return DocumentType.MedicalReport;
        if (tokens.Contains("garage") || tokens.Contains("estimate") || tokens.Contains("quotation") || tokens.Contains("quote")) return DocumentType.GarageEstimate;
        if (tokens.Contains("photo") || tokens.Contains("photos") || tokens.Contains("damage")) return DocumentType.PhotoEvidence;
        if (tokens.Contains("id") || tokens.Contains("nin") || tokens.Contains("passport") || tokens.Contains("licence") || tokens.Contains("license")) return DocumentType.CustomerId;
        return DocumentType.Unknown;
    }
}
