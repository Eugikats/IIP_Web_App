using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Application.Documents;

/// <summary>Outcome K acceptance: "accuracy per document type is measured and visible." Thin
/// Application-layer wrapper: permission check, tenant scope, then the Domain rule
/// (DocumentClassificationAccuracy) that does the actual, deterministic computation.</summary>
public sealed class DocumentQualityService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IDocumentLabelledExampleRepository labelledExampleRepository) : IDocumentQualityService
{
    public async Task<IReadOnlyList<DocumentTypeAccuracyDto>> GetAccuracyByTypeAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var context = tenantContextAccessor.Current;
        var scope = Domain.Common.TenantScope.Create(context.TenantId, context.WorkspaceId);
        var examples = await labelledExampleRepository.ListAsync(scope, cancellationToken);

        return DocumentClassificationAccuracy.ByDocumentType(examples)
            .Select(row => new DocumentTypeAccuracyDto(
                row.DocumentType.ToString(),
                row.SampleCount,
                row.CorrectCount,
                row.Accuracy,
                DocumentClassificationAccuracy.MinimumSampleCount))
            .ToArray();
    }
}
