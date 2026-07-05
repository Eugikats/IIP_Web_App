using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.Documents;

/// <summary>
/// Cross-case document library for the current tenant/workspace. Read-only,
/// permission-gated, tenant-scoped. The per-case document workspace remains the
/// place to act on a document; this is the aggregate view.
/// </summary>
public sealed class DocumentQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IDocumentReadRepository repository) : IDocumentQueryService
{
    public async Task<IReadOnlyList<DocumentLibraryItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewAllClaims, cancellationToken);

        var context = tenantContextAccessor.Current;
        return await repository.ListAllAsync(context.TenantId, context.WorkspaceId, cancellationToken);
    }
}
