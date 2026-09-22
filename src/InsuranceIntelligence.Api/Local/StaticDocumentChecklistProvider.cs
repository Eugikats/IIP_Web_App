using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Api.Local;

/// <summary>productRepository is optional so every existing caller keeps compiling and behaving
/// unchanged; omitting it simply means the built-in claim-type rules are always used, exactly the
/// prior behavior. When present, a claim pinned to a configured product (outcome A/C) checks
/// against that product's own document matrix instead, the whole point of letting an administrator
/// configure it with no developer and no release.</summary>
public sealed class StaticDocumentChecklistProvider(
    ICompletenessScoringService scoringService,
    IProductRepository? productRepository = null) : IDocumentChecklistProvider
{
    public async Task<DocumentCompletenessChecklist> BuildChecklistAsync(
        ClaimCase claimCase,
        IReadOnlyCollection<CaseDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var rules = await ResolveRulesAsync(claimCase, cancellationToken);
        var received = documents
            .Where(document => document.ProcessingStatus is DocumentProcessingStatus.Processed or DocumentProcessingStatus.ExtractionApproved)
            .Select(document => document.DocumentType)
            .Distinct()
            .ToArray();
        var score = scoringService.Calculate(rules.RequiredDocuments, received);
        var missing = rules.RequiredDocuments
            .Where(required => !received.Contains(required))
            .Select(required => new MissingDocumentRequirement(required, LabelFor(required), Required: true))
            .ToArray();

        return new DocumentCompletenessChecklist(
            claimCase.CaseId,
            claimCase.ProductLine,
            rules.RequiredDocuments,
            rules.OptionalDocuments,
            received,
            missing,
            score);
    }

    private async Task<DocumentChecklistRuleSet> ResolveRulesAsync(ClaimCase claimCase, CancellationToken cancellationToken)
    {
        if (productRepository is not null && claimCase.ProductId is { } productId && claimCase.ProductVersion is { } version)
        {
            var product = await productRepository.GetVersionAsync(claimCase.Scope, productId, version, cancellationToken);
            var productRules = product is null ? null : DocumentChecklistRules.ForProduct(product);
            if (productRules is not null)
            {
                return productRules;
            }
        }

        return DocumentChecklistRules.ForCase(claimCase);
    }

    public static string LabelFor(DocumentType documentType) =>
        documentType switch
        {
            DocumentType.ClaimForm => "Claim form",
            DocumentType.PoliceAbstract => "Police abstract",
            DocumentType.Invoice => "Invoice",
            DocumentType.MedicalReport => "Medical report",
            DocumentType.PhotoEvidence => "Photo evidence",
            DocumentType.CustomerId => "Customer ID",
            DocumentType.GarageEstimate => "Garage estimate",
            _ => "Unknown document"
        };
}
