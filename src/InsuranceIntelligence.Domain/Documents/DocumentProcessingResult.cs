namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentProcessingResult(
    DocumentClassificationResult Classification,
    DocumentExtractionResult Extraction,
    IReadOnlyCollection<DocumentValidationWarning> ValidationWarnings);
