using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentChecklistRuleSet(
    IReadOnlyCollection<DocumentType> RequiredDocuments,
    IReadOnlyCollection<DocumentType> OptionalDocuments,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomRequiredDocuments = null,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomOptionalDocuments = null)
{
    /// <summary>Never null, unlike the constructor parameter: existing call sites
    /// (<see cref="DocumentChecklistRules.ForProductLine"/>, <see cref="DocumentChecklistRules.Resolve"/>)
    /// never pass one, and a reader should not have to know that.</summary>
    public IReadOnlyCollection<CustomDocumentRequirement> CustomRequired => CustomRequiredDocuments ?? [];

    public IReadOnlyCollection<CustomDocumentRequirement> CustomOptional => CustomOptionalDocuments ?? [];
}

public static class DocumentChecklistRules
{
    public static DocumentChecklistRuleSet ForProductLine(string productLine)
    {
        if (string.Equals(productLine, "Motor", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentChecklistRuleSet(
                [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence],
                [DocumentType.Invoice, DocumentType.GarageEstimate, DocumentType.CustomerId]);
        }

        if (string.Equals(productLine, "Medical", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentChecklistRuleSet(
                [DocumentType.ClaimForm, DocumentType.MedicalReport, DocumentType.Invoice],
                [DocumentType.CustomerId]);
        }

        return new DocumentChecklistRuleSet(
            [DocumentType.ClaimForm],
            [DocumentType.CustomerId]);
    }

    /// <summary>
    /// Claim-type discriminator on the single governed path. Falls back to product-line rules
    /// when the claim type is unknown, including for every non-Motor product line, since the
    /// own-damage/third-party/theft/injury taxonomy only applies to Motor (ClaimTypeClassifier
    /// returns Unknown for other product lines).
    /// </summary>
    public static DocumentChecklistRuleSet ForCase(ClaimCase claimCase)
    {
        ArgumentNullException.ThrowIfNull(claimCase);
        return Resolve(claimCase.ProductLine, claimCase.ClaimType, claimCase.MotorFnolDetails);
    }

    /// <summary>
    /// An administrator's configured document matrix (outcome A) for the claim's pinned product.
    /// Every entry's DocumentKey was already checked against the tenant's document type catalog
    /// when the product was saved (ProductCommandService), so nothing here is refused; a key that
    /// still does not match a built-in DocumentType enum name is a real custom document type,
    /// tracked by key rather than silently dropped, which is the fix for the gap this method's
    /// previous version had: a typo used to just vanish from the checklist with no warning.
    /// If nothing in the matrix maps at all, the caller should fall back to the built-in
    /// claim-type rules rather than show an empty checklist for a genuinely configured product
    /// (see StaticDocumentChecklistProvider).
    /// </summary>
    public static DocumentChecklistRuleSet? ForProduct(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (product.DocumentMatrix.Count == 0)
        {
            return null;
        }

        var required = new List<DocumentType>();
        var optional = new List<DocumentType>();
        var customRequired = new List<CustomDocumentRequirement>();
        var customOptional = new List<CustomDocumentRequirement>();
        foreach (var entry in product.DocumentMatrix)
        {
            if (Enum.TryParse<DocumentType>(entry.DocumentKey, ignoreCase: true, out var documentType))
            {
                (entry.IsRequired ? required : optional).Add(documentType);
            }
            else
            {
                var custom = new CustomDocumentRequirement(entry.DocumentKey.Trim(), entry.CustomerLabel);
                (entry.IsRequired ? customRequired : customOptional).Add(custom);
            }
        }

        if (required.Count == 0 && optional.Count == 0 && customRequired.Count == 0 && customOptional.Count == 0)
        {
            return null;
        }

        return new DocumentChecklistRuleSet(
            required.Distinct().ToArray(),
            optional.Distinct().ToArray(),
            customRequired.DistinctBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToArray(),
            customOptional.DistinctBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>
    /// Same resolution as <see cref="ForCase"/> but from the raw FNOL fields directly, so a
    /// document checklist can be previewed before a case exists (e.g. FNOL intake).
    /// </summary>
    public static DocumentChecklistRuleSet Resolve(string productLine, string? claimType, MotorFnolDetails? motorFnolDetails)
    {
        var kind = ClaimTypeClassifier.Classify(claimType, motorFnolDetails, productLine);
        return kind switch
        {
            ClaimTypeKind.OwnDamage => ForProductLine("Motor"),
            ClaimTypeKind.ThirdPartyProperty => new DocumentChecklistRuleSet(
                [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence, DocumentType.CustomerId],
                [DocumentType.Invoice, DocumentType.GarageEstimate]),
            ClaimTypeKind.TheftTotalLoss => new DocumentChecklistRuleSet(
                [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence, DocumentType.CustomerId],
                [DocumentType.Invoice, DocumentType.GarageEstimate]),
            ClaimTypeKind.InjuryFatality => new DocumentChecklistRuleSet(
                [DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence, DocumentType.MedicalReport],
                [DocumentType.CustomerId, DocumentType.Invoice]),
            _ => ForProductLine(productLine)
        };
    }
}
