namespace InsuranceIntelligence.Domain.Documents;

public interface IDocumentProcessingProvider
{
    Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default);
}
