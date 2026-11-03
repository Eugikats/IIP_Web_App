using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>The words a customer sees for each built-in document type when an insurer has not
/// configured their own. One table, used by the customer page, the staff checklist and the
/// built-in rules.</summary>
public static class BuiltInDocumentWording
{
    public static string Label(DocumentType documentType) =>
        documentType switch
        {
            DocumentType.ClaimForm => "Completed claim form",
            DocumentType.PoliceAbstract => "Police report",
            DocumentType.PhotoEvidence => "Photos of the damage",
            DocumentType.Invoice => "Repair estimate or invoice",
            DocumentType.MedicalReport => "Medical report",
            DocumentType.CustomerId => "Customer identification",
            DocumentType.GarageEstimate => "Garage estimate",
            _ => "Supporting document"
        };

    public static string Description(DocumentType documentType) =>
        documentType switch
        {
            DocumentType.ClaimForm => "The completed claim form for this loss.",
            DocumentType.PoliceAbstract => "Confirms the incident was reported to the police.",
            DocumentType.PhotoEvidence => "Photos showing what happened.",
            DocumentType.Invoice => "Shows the cost of repair, replacement or treatment.",
            DocumentType.MedicalReport => "A doctor's account of the injury or treatment.",
            DocumentType.CustomerId => "Confirms the claim is yours.",
            DocumentType.GarageEstimate => "A garage's estimate to repair the vehicle.",
            _ => "A document that supports your claim."
        };
}

public sealed record DocumentChecklistRuleSet(
    IReadOnlyCollection<DocumentType> RequiredDocuments,
    IReadOnlyCollection<DocumentType> OptionalDocuments,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomRequiredDocuments = null,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomOptionalDocuments = null,
    IReadOnlyList<ResolvedDocumentSlot>? ConfiguredSlots = null)
{
    /// <summary>Never null, unlike the constructor parameter: existing call sites
    /// (<see cref="DocumentChecklistRules.ForProductLine"/>, <see cref="DocumentChecklistRules.Resolve"/>)
    /// never pass one, and a reader should not have to know that.</summary>
    public IReadOnlyCollection<CustomDocumentRequirement> CustomRequired => CustomRequiredDocuments ?? [];

    public IReadOnlyCollection<CustomDocumentRequirement> CustomOptional => CustomOptionalDocuments ?? [];

    /// <summary>The slots a customer can fill, in the order shown: required first, then optional.
    /// From the claim's pinned product when one is configured; otherwise built from the built-in
    /// document types with their default wording, kind and limits.</summary>
    public IReadOnlyList<ResolvedDocumentSlot> Slots => ConfiguredSlots ?? BuiltInSlots();

    private IReadOnlyList<ResolvedDocumentSlot> BuiltInSlots() =>
        RequiredDocuments.Select(type => DocumentSlotDefaults.ForBuiltIn(type, BuiltInDocumentWording.Label(type), BuiltInDocumentWording.Description(type), required: true))
            .Concat(CustomRequired.Select(custom => CustomSlot(custom, required: true)))
            .Concat(OptionalDocuments.Select(type => DocumentSlotDefaults.ForBuiltIn(type, BuiltInDocumentWording.Label(type), BuiltInDocumentWording.Description(type), required: false)))
            .Concat(CustomOptional.Select(custom => CustomSlot(custom, required: false)))
            .ToArray();

    private static ResolvedDocumentSlot CustomSlot(CustomDocumentRequirement custom, bool required) =>
        new(custom.Key, custom.Label, custom.Description, required, DocumentKind.Document, false,
            DocumentSlotDefaults.MaxFilesFor(DocumentKind.Document, false), DocumentSlotDefaults.DocumentFormats);
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
    /// An administrator's configured document matrix for the claim's pinned product and claim type
    /// (build-loop item 4). A requirement applies to a claim when its <c>ClaimTypeKeys</c> is empty
    /// (every claim type) or contains the claim's type key; a claim with no resolved claim type
    /// sees only the requirements that apply to every type. Every entry's DocumentKey was already
    /// checked against the tenant's document type catalog when the product was saved
    /// (ProductCommandService), so nothing here is refused; a key that does not match a built-in
    /// DocumentType enum name is a real custom document type, tracked by key rather than silently
    /// dropped. Null means the product has no matrix at all and the caller should fall back to the
    /// built-in claim-type rules (see ProductAwareDocumentChecklistProvider); a matrix that has
    /// nothing for this claim type yields an empty set.
    /// </summary>
    public static DocumentChecklistRuleSet? ForProduct(Product product, string? claimTypeKey = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (product.DocumentMatrix.Count == 0)
        {
            // Nothing configured at all: the caller falls back to the built-in rules.
            return null;
        }

        var applicable = product.DocumentMatrix
            .Where(entry => entry.AppliesTo(claimTypeKey))
            .ToArray();
        if (applicable.Length == 0)
        {
            // The insurer configured documents, but none for this claim type. That is their
            // answer ("nothing is required for this type"), not a gap to paper over with the
            // built-in Motor rules, so the claim gets an empty set and the editor warns about it.
            return new DocumentChecklistRuleSet([], [], [], [], []);
        }

        // Two entries for one document key can both apply to a claim type (one for every type,
        // one for this type). The more specific one (it names the claim type) wins.
        var chosen = applicable
            .GroupBy(entry => entry.DocumentKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.ClaimTypeKeys is { Count: > 0 } ? 1 : 0).First())
            .ToArray();

        var required = new List<DocumentType>();
        var optional = new List<DocumentType>();
        var customRequired = new List<CustomDocumentRequirement>();
        var customOptional = new List<CustomDocumentRequirement>();
        var slots = new List<ResolvedDocumentSlot>();
        foreach (var entry in chosen)
        {
            slots.Add(entry.ToSlot());
            if (Enum.TryParse<DocumentType>(entry.DocumentKey, ignoreCase: true, out var documentType))
            {
                (entry.IsRequired ? required : optional).Add(documentType);
            }
            else
            {
                var custom = new CustomDocumentRequirement(entry.DocumentKey.Trim(), entry.CustomerLabel, entry.Reason);
                (entry.IsRequired ? customRequired : customOptional).Add(custom);
            }
        }

        return new DocumentChecklistRuleSet(
            required.Distinct().ToArray(),
            optional.Distinct().ToArray(),
            customRequired.ToArray(),
            customOptional.ToArray(),
            slots.OrderBy(slot => slot.Required ? 0 : 1).ToArray());
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
