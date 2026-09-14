using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentChecklistRuleSet(
    IReadOnlyCollection<DocumentType> RequiredDocuments,
    IReadOnlyCollection<DocumentType> OptionalDocuments);

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
