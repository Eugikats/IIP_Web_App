namespace InsuranceIntelligence.Application.Documents;

public sealed record DocumentTypeAccuracyDto(
    string DocumentType,
    int SampleCount,
    int CorrectCount,
    decimal? Accuracy,
    int MinimumSampleCount);

public interface IDocumentQualityService
{
    Task<IReadOnlyList<DocumentTypeAccuracyDto>> GetAccuracyByTypeAsync(CancellationToken cancellationToken = default);
}
