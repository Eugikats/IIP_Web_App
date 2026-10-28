using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class MockDocumentProcessingProvider : IDocumentProcessingProvider
{
    public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
    {
        var documentType = DocumentContentClassifier.Classify(request.OriginalFileName).Type;
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

}
