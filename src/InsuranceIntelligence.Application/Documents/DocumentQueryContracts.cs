namespace InsuranceIntelligence.Application.Documents;

public interface IDocumentQueryService
{
    Task<IReadOnlyList<DocumentLibraryItemDto>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IDocumentReadRepository
{
    Task<IReadOnlyList<DocumentLibraryItemDto>> ListAllAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentLibraryItemDto(
    string DocumentId,
    string CaseId,
    string FileName,
    string DocumentType,
    string Status,
    decimal ClassificationConfidence,
    string UploadedAt);
