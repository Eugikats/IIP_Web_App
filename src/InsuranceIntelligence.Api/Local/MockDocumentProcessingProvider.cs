using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Local;

public sealed class MockDocumentProcessingProvider : IDocumentProcessingProvider
{
    public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
    {
        var documentType = Classify(request.OriginalFileName);
        var confidence = documentType == DocumentType.Unknown ? 0.35m : 0.92m;
        var now = DateTimeOffset.UtcNow;
        var fields = new Dictionary<string, string>
        {
            ["sourceFileName"] = request.OriginalFileName,
            ["mockDocumentType"] = documentType.ToString(),
            ["caseId"] = request.CaseId.ToString(),
            ["objectKey"] = request.ObjectKey
        };
        var fieldConfidences = fields.Keys.ToDictionary(key => key, _ => confidence);
        var warnings = new List<DocumentValidationWarning>();

        if (documentType == DocumentType.Unknown)
        {
            warnings.Add(new DocumentValidationWarning(
                "document.type.unknown",
                "The mock classifier could not confidently identify this document type.",
                "warning"));
        }

        var result = new DocumentProcessingResult(
            new DocumentClassificationResult(documentType, confidence, "Mock", now),
            new DocumentExtractionResult(fields, fieldConfidences, "Mock", now, Approved: false, ApprovedByUserId: null, ApprovedAt: null),
            warnings);

        return Task.FromResult(result);
    }

    private static DocumentType Classify(string fileName)
    {
        var normalized = fileName.ToLowerInvariant();

        if (normalized.Contains("claim", StringComparison.Ordinal))
        {
            return DocumentType.ClaimForm;
        }

        if (normalized.Contains("police", StringComparison.Ordinal))
        {
            return DocumentType.PoliceAbstract;
        }

        if (normalized.Contains("invoice", StringComparison.Ordinal))
        {
            return DocumentType.Invoice;
        }

        if (normalized.Contains("medical", StringComparison.Ordinal))
        {
            return DocumentType.MedicalReport;
        }

        if (normalized.Contains("photo", StringComparison.Ordinal))
        {
            return DocumentType.PhotoEvidence;
        }

        if (normalized.Contains("id", StringComparison.Ordinal))
        {
            return DocumentType.CustomerId;
        }

        return DocumentType.Unknown;
    }
}
