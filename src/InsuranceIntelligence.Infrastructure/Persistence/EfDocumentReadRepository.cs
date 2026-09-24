using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Cross-case document library read repository, lists all case documents for a
/// tenant/workspace, newest first.
/// </summary>
public sealed class EfDocumentReadRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IDocumentReadRepository
{
    public async Task<IReadOnlyList<DocumentLibraryItemDto>> ListAllAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);
        var payloads = await Scoped(DbContext.CaseDocuments.AsNoTracking(), scope)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        return payloads
            .Select(Deserialize<CaseDocument>)
            .OrderByDescending(document => document.UploadedAt)
            .Select(document => new DocumentLibraryItemDto(
                document.DocumentId.ToString(),
                document.CaseId.ToString(),
                document.OriginalFileName,
                document.DocumentType.ToString(),
                document.ProcessingStatus.ToString(),
                document.Classification.ConfidenceScore,
                document.UploadedAt.ToString("O")))
            .ToArray();
    }
}
