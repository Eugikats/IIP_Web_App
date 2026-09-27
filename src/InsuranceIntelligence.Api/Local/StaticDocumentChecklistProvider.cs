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
        var processed = documents
            .Where(document => !document.IsSuperseded
                && document.ProcessingStatus is DocumentProcessingStatus.Processed or DocumentProcessingStatus.ExtractionApproved)
            .ToArray();
        var received = processed
            .Select(document => document.DocumentType)
            .Distinct()
            .ToArray();
        // A custom document type has no DocumentType of its own, so it is matched on the key the
        // customer chose (outcome B), falling back to the classifier's DocumentType name for a
        // document nobody was asked to name (see CaseDocument.DocumentTypeKey).
        var receivedKeys = processed
            .Select(document => document.DocumentTypeKey ?? document.DocumentType.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var customReceivedCount = rules.CustomRequired.Count(entry => receivedKeys.Contains(entry.Key));
        var score = scoringService.Calculate(rules.RequiredDocuments, received, rules.CustomRequired.Count, customReceivedCount);

        var missing = rules.RequiredDocuments
            .Where(required => !received.Contains(required))
            .Select(required => new MissingDocumentRequirement(required.ToString(), LabelFor(required), Required: true))
            .Concat(rules.CustomRequired
                .Where(custom => !receivedKeys.Contains(custom.Key))
                .Select(custom => new MissingDocumentRequirement(custom.Key, custom.Label, Required: true)))
            .ToArray();

        return new DocumentCompletenessChecklist(
            claimCase.CaseId,
            claimCase.ProductLine,
            rules.RequiredDocuments,
            rules.OptionalDocuments,
            received,
            missing,
            score,
            rules.CustomRequired,
            rules.CustomOptional,
            rules.CustomRequired.Where(entry => receivedKeys.Contains(entry.Key)).Select(entry => entry.Key).ToArray());
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
