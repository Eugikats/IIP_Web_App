namespace InsuranceIntelligence.Domain.Documents;

/// <summary>Key is a catalog key (claim intake build prompt outcome A): the built-in
/// <see cref="Documents.DocumentType"/> name for one of the eight recognised types, or a tenant's
/// own key for a custom document type. String, not the enum, so a custom document type can be
/// represented as a missing requirement too.</summary>
public sealed record MissingDocumentRequirement(
    string Key,
    string Label,
    bool Required);
