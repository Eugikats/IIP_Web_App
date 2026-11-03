using System.Text;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// Deterministic stand-in for Azure Document Intelligence, for local runs and tests only.
///
/// Classification is read from a marker inside the file content: the ASCII text
/// <c>IIP-MOCK-DOC:TypeKey</c> anywhere in the first 4 MB (a text file, a PDF comment, or bytes
/// appended to an image all work), where TypeKey is a <see cref="DocumentType"/> name. Extra
/// fields can be planted with <c>IIP-MOCK-FIELD:key=value</c>. With no marker the file name is
/// matched on whole tokens, and anything else is Unknown. Nothing here looks at real content, so a
/// fixture always produces the same result.
/// </summary>
public sealed class MockDocumentProcessingProvider : IDocumentProcessingProvider
{
    public const string ProviderName = "Mock";
    public const string DocMarker = "IIP-MOCK-DOC:";
    public const string FieldMarker = "IIP-MOCK-FIELD:";
    private const int ScanBytes = 4 * 1024 * 1024;

    public async Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
    {
        var scanned = await ReadHeadAsync(request, cancellationToken);
        var markerType = ParseMarkerType(scanned);
        var documentType = markerType ?? ClassifyFileName(request.OriginalFileName);
        var confidence = markerType is not null ? 0.96m : documentType == DocumentType.Unknown ? 0.30m : 0.78m;
        var now = DateTimeOffset.UtcNow;

        var fields = new Dictionary<string, string>
        {
            ["sourceFileName"] = request.OriginalFileName,
            ["objectKey"] = request.ObjectKey,
            ["contentType"] = request.ContentType,
            ["mockDocumentType"] = documentType.ToString()
        };
        foreach (var (key, value) in DefaultFieldsFor(documentType))
        {
            fields[$"field.{key}"] = value;
        }

        foreach (var (key, value) in ParseMarkerFields(scanned))
        {
            fields[$"field.{key}"] = value;
        }

        var confidences = fields.ToDictionary(
            pair => pair.Key,
            pair => pair.Key.StartsWith("field.", StringComparison.Ordinal) ? Math.Min(confidence, 0.91m) : confidence);

        var warnings = new List<DocumentValidationWarning>();
        if (documentType == DocumentType.Unknown)
        {
            warnings.Add(new DocumentValidationWarning(
                "document.type.unknown",
                "The mock classifier could not identify this document type.",
                "warning"));
        }

        return new DocumentProcessingResult(
            new DocumentClassificationResult(documentType, confidence, ProviderName, now, "mock-v1"),
            new DocumentExtractionResult(fields, confidences, ProviderName, now, Approved: false, ApprovedByUserId: null, ApprovedAt: null, ModelVersion: "mock-v1"),
            warnings);
    }

    /// <summary>Whole-token file name match: "police_letter.pdf" is a police abstract, but
    /// "accident_report.pdf" is not a customer ID just because it contains the letters "id".</summary>
    public static DocumentType ClassifyFileName(string fileName)
    {
        var tokens = Path.GetFileNameWithoutExtension(fileName)
            .Split(new[] { '_', '-', ' ', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant())
            .ToHashSet();

        if (tokens.Contains("claim") && tokens.Contains("form")) return DocumentType.ClaimForm;
        if (tokens.Contains("police") || tokens.Contains("abstract")) return DocumentType.PoliceAbstract;
        if (tokens.Contains("invoice") || tokens.Contains("receipt")) return DocumentType.Invoice;
        if (tokens.Contains("medical")) return DocumentType.MedicalReport;
        if (tokens.Contains("garage") || tokens.Contains("estimate")) return DocumentType.GarageEstimate;
        if (tokens.Contains("photo") || tokens.Contains("damage")) return DocumentType.PhotoEvidence;
        if (tokens.Contains("id") || tokens.Contains("passport") || tokens.Contains("licence") || tokens.Contains("license")) return DocumentType.CustomerId;
        return DocumentType.Unknown;
    }

    private static IEnumerable<(string Key, string Value)> DefaultFieldsFor(DocumentType type) => type switch
    {
        DocumentType.PoliceAbstract => new[]
        {
            ("ReportNumber", "MOCK-POL-0001"),
            ("ReportDate", "2026-10-01"),
            ("Station", "Mock Central Police Station"),
        },
        DocumentType.ClaimForm => new[]
        {
            ("PolicyNumber", "MOCK-POLICY-0001"),
            ("IncidentDate", "2026-10-01"),
            ("ClaimantName", "Mock Claimant"),
        },
        DocumentType.Invoice => new[]
        {
            ("InvoiceTotal", "1500000"),
            ("Currency", "UGX"),
            ("VendorName", "Mock Garage Ltd"),
        },
        DocumentType.GarageEstimate => new[]
        {
            ("EstimateTotal", "1800000"),
            ("Currency", "UGX"),
            ("VendorName", "Mock Garage Ltd"),
        },
        DocumentType.MedicalReport => new[]
        {
            ("PatientName", "Mock Patient"),
            ("TreatmentDate", "2026-10-01"),
            ("Facility", "Mock Health Centre"),
        },
        DocumentType.CustomerId => new[]
        {
            ("FullName", "Mock Claimant"),
            ("IdNumber", "MOCK-ID-0001"),
        },
        _ => Array.Empty<(string, string)>(),
    };

    private static async Task<string> ReadHeadAsync(DocumentProcessingRequest request, CancellationToken cancellationToken)
    {
        if (request.OpenContentStreamAsync is null)
        {
            return string.Empty;
        }

        try
        {
            await using var stream = await request.OpenContentStreamAsync(cancellationToken);
            var buffer = new byte[ScanBytes];
            var total = 0;
            int read;
            while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)) > 0)
            {
                total += read;
            }

            return Encoding.Latin1.GetString(buffer, 0, total);
        }
        catch (NotSupportedException)
        {
            return string.Empty;
        }
    }

    private static DocumentType? ParseMarkerType(string scanned)
    {
        var index = scanned.IndexOf(DocMarker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + DocMarker.Length;
        var end = start;
        while (end < scanned.Length && (char.IsLetterOrDigit(scanned[end]) || scanned[end] == '_'))
        {
            end++;
        }

        return Enum.TryParse<DocumentType>(scanned[start..end], ignoreCase: true, out var parsed) ? parsed : null;
    }

    private static IEnumerable<(string Key, string Value)> ParseMarkerFields(string scanned)
    {
        var from = 0;
        while (true)
        {
            var index = scanned.IndexOf(FieldMarker, from, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            var start = index + FieldMarker.Length;
            var end = scanned.IndexOfAny(new[] { '\r', '\n', '\0' }, start);
            if (end < 0)
            {
                end = scanned.Length;
            }

            var pair = scanned[start..end].Trim();
            var equals = pair.IndexOf('=');
            if (equals > 0 && equals < pair.Length - 1)
            {
                yield return (pair[..equals].Trim(), pair[(equals + 1)..].Trim());
            }

            from = end;
        }
    }
}
