namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentCompletenessChecklist(
    Guid CaseId,
    string ProductLine,
    IReadOnlyCollection<DocumentType> RequiredDocuments,
    IReadOnlyCollection<DocumentType> OptionalDocuments,
    IReadOnlyCollection<DocumentType> ReceivedDocuments,
    IReadOnlyCollection<MissingDocumentRequirement> MissingRequiredDocuments,
    CompletenessScore CompletenessScore);
