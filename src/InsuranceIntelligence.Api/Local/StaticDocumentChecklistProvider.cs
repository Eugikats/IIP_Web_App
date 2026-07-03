using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Local;

public sealed class StaticDocumentChecklistProvider(ICompletenessScoringService scoringService) : IDocumentChecklistProvider
{
    public Task<DocumentCompletenessChecklist> BuildChecklistAsync(
        ClaimCase claimCase,
        IReadOnlyCollection<CaseDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var rules = DocumentChecklistRules.ForProductLine(claimCase.ProductLine);
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

        var checklist = new DocumentCompletenessChecklist(
            claimCase.CaseId,
            claimCase.ProductLine,
            rules.RequiredDocuments,
            rules.OptionalDocuments,
            received,
            missing,
            score);

        return Task.FromResult(checklist);
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
