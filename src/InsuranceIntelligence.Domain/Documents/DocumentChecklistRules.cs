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
}
