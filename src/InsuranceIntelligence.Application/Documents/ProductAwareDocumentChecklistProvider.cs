using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Documents;

/// <summary>The completeness arithmetic, shared by every host (the API and the Functions app) so
/// both count a claim's documents the same way.</summary>
public class DocumentCompletenessScorer : ICompletenessScoringService
{
    public CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments) =>
        CompletenessScore.Calculate(requiredDocuments, receivedDocuments);

    public CompletenessScore Calculate(
        IReadOnlyCollection<DocumentType> requiredDocuments,
        IReadOnlyCollection<DocumentType> receivedDocuments,
        int customRequiredTotal,
        int customReceived) =>
        CompletenessScore.Calculate(requiredDocuments, receivedDocuments, customRequiredTotal, customReceived);
}

/// <summary>
/// Builds a claim's document checklist from the claim's pinned product version and claim type, with
/// the built-in Motor/Medical rules as the fallback when no product is configured or nothing in it
/// applies. It lives in Application so the API, the follow-through timer and every other host ask the
/// same question and get the same slots, wording and completeness (build-loop item 4).
/// <para>productRepository is optional so a caller with no product configuration behaves exactly as
/// before: the built-in claim-type rules are always used.</para>
/// </summary>
public class ProductAwareDocumentChecklistProvider(
    ICompletenessScoringService scoringService,
    IProductRepository? productRepository = null) : IDocumentChecklistProvider
{
    public async Task<DocumentCompletenessChecklist> BuildChecklistAsync(
        ClaimCase claimCase,
        IReadOnlyCollection<CaseDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var rules = await ResolveRulesAsync(claimCase, cancellationToken);

        // One completeness rule for every channel and every document kind: a required slot counts
        // once a file is stored in it (the customer's choice of slot, or the classifier's type when
        // nobody chose one) and it has not failed, been rejected or been superseded. Whether the
        // classifier agrees with the customer is a flag for staff, not a reason to say "missing".
        var counted = documents.Where(document => document.CountsTowardsCompleteness).ToArray();
        var receivedKeys = counted
            .Select(document => document.EffectiveKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var received = receivedKeys
            .Select(key => Enum.TryParse<DocumentType>(key, ignoreCase: true, out var type) ? type : DocumentType.Unknown)
            .Where(type => type != DocumentType.Unknown)
            .Distinct()
            .ToArray();
        var processed = documents
            .Where(document => !document.IsSuperseded
                && document.ProcessingStatus is DocumentProcessingStatus.Processed or DocumentProcessingStatus.ExtractionApproved)
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
        var customReceivedCount = rules.CustomRequired.Count(entry => receivedKeys.Contains(entry.Key));
        var score = scoringService.Calculate(rules.RequiredDocuments, received, rules.CustomRequired.Count, customReceivedCount);

        var slots = rules.Slots;
        var labels = slots.ToDictionary(slot => slot.Key, slot => slot.Label, StringComparer.OrdinalIgnoreCase);
        var missing = rules.RequiredDocuments
            .Where(required => !received.Contains(required))
            .Select(required => new MissingDocumentRequirement(
                required.ToString(),
                labels.TryGetValue(required.ToString(), out var label) ? label : LabelFor(required),
                Required: true))
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
            fieldChecks,
            slots);
    }

    private async Task<DocumentChecklistRuleSet> ResolveRulesAsync(ClaimCase claimCase, CancellationToken cancellationToken)
    {
        if (productRepository is not null && claimCase.ProductId is { } productId && claimCase.ProductVersion is { } version)
        {
            var product = await productRepository.GetVersionAsync(claimCase.Scope, productId, version, cancellationToken);
            if (product is not null)
            {
                // A claim filed before claim types existed (or by a channel that could not choose
                // one) has no key; resolve it from the text so it still gets its type's documents.
                var claimTypeKey = claimCase.ClaimTypeKey ?? product.FindClaimType(claimCase.ClaimType)?.Key;
                var productRules = DocumentChecklistRules.ForProduct(product, claimTypeKey);
                if (productRules is not null)
                {
                    return productRules;
                }
            }
        }

        return DocumentChecklistRules.ForCase(claimCase);
    }

    public static DocumentType EffectiveType(CaseDocument document) =>
        document.DocumentTypeKey is { Length: > 0 } key
        && Enum.GetNames<DocumentType>().Contains(key.Trim(), StringComparer.OrdinalIgnoreCase)
        && Enum.Parse<DocumentType>(key.Trim(), ignoreCase: true) is var named and not DocumentType.Unknown
            ? named
            : document.DocumentType;

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
