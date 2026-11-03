using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Api.Local;

/// <summary>
/// API compatibility name for the shared product-aware checklist provider. The implementation is
/// in Application so API and Functions use the same pinned-product, claim-type, slot and field rules.
/// </summary>
public sealed class StaticDocumentChecklistProvider(
    ICompletenessScoringService scoringService,
    IProductRepository? productRepository = null)
    : ProductAwareDocumentChecklistProvider(scoringService, productRepository)
{
    public static new DocumentType EffectiveType(CaseDocument document) =>
        ProductAwareDocumentChecklistProvider.EffectiveType(document);

    public static new string LabelFor(DocumentType documentType) =>
        ProductAwareDocumentChecklistProvider.LabelFor(documentType);
}
