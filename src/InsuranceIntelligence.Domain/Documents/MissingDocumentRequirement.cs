namespace InsuranceIntelligence.Domain.Documents;

public sealed record MissingDocumentRequirement(
    DocumentType DocumentType,
    string Label,
    bool Required);
