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
        // Receipt and processing are separate facts. A file that was durably uploaded into a
        // selected slot counts as received even when OCR is not configured; its classification
        // and extraction remain untouched and can still be Unknown/NotConfigured.
        var receivedDocuments = documents
            .Where(document => !document.IsSuperseded
                && document.ProcessingStatus is DocumentProcessingStatus.Uploaded
                    or DocumentProcessingStatus.Processed
                    or DocumentProcessingStatus.ExtractionApproved)
            .ToArray();
        var processed = documents
            .Where(document => !document.IsSuperseded
                && document.ProcessingStatus is DocumentProcessingStatus.Processed
                    or DocumentProcessingStatus.ExtractionApproved)
            .ToArray();
        // Which document this is follows the customer's own choice of slot when they made one and it names a built-in
        // type, because they know what they are holding and a scan called scan001.pdf tells the classifier nothing.
        // The classifier's independent guess is kept on the document so a disagreement can be shown to a person.
        var received = receivedDocuments
            .Select(EffectiveType)
            .Where(documentType => documentType != DocumentType.Unknown)
            .Distinct()
            .ToArray();
        var fieldChecks = processed
            .GroupBy(EffectiveType)
            .Select(group => group
                .Select(document => DocumentFieldCheck.Check(document, group.Key, claimCase.ProductLine, LabelFor(group.Key)))
                .OrderByDescending(check => check.Readable)
                .ThenByDescending(check => check.Found.Count)
                .First())
            .Where(check => check.DocumentType != DocumentType.Unknown)
            .ToArray();
        // A custom document type has no DocumentType of its own, so it is matched on the key the
        // customer chose (outcome B), falling back to the classifier's DocumentType name for a
        // document nobody was asked to name (see CaseDocument.DocumentTypeKey).
        var receivedKeys = receivedDocuments
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
            rules.CustomRequired.Where(entry => receivedKeys.Contains(entry.Key)).Select(entry => entry.Key).ToArray(),
            fieldChecks);
    }

    /// <summary>The built-in type the customer named for this document, else the classifier's type.</summary>
    public static DocumentType EffectiveType(CaseDocument document) =>
        document.DocumentTypeKey is { Length: > 0 } key
        && Enum.GetNames<DocumentType>().Contains(key.Trim(), StringComparer.OrdinalIgnoreCase)
        && Enum.Parse<DocumentType>(key.Trim(), ignoreCase: true) is var named and not DocumentType.Unknown
            ? named
            : document.DocumentType;

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
