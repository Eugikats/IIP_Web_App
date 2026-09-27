namespace InsuranceIntelligence.Domain.Documents;

/// <summary>The Custom* fields (claim intake build prompt outcome A) cover a product's document
/// types that are not one of the eight built-in <see cref="DocumentType"/> values. They default to
/// empty so every existing construction (built before custom types existed) keeps compiling and
/// behaving exactly as before. A custom requirement that is missing is already folded into
/// <see cref="MissingRequiredDocuments"/>, so the existing readers of that list need no changes.</summary>
public sealed record DocumentCompletenessChecklist(
    Guid CaseId,
    string ProductLine,
    IReadOnlyCollection<DocumentType> RequiredDocuments,
    IReadOnlyCollection<DocumentType> OptionalDocuments,
    IReadOnlyCollection<DocumentType> ReceivedDocuments,
    IReadOnlyCollection<MissingDocumentRequirement> MissingRequiredDocuments,
    CompletenessScore CompletenessScore,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomRequiredDocuments = null,
    IReadOnlyCollection<CustomDocumentRequirement>? CustomOptionalDocuments = null,
    IReadOnlyCollection<string>? CustomReceivedKeys = null)
{
    public IReadOnlyCollection<CustomDocumentRequirement> CustomRequired => CustomRequiredDocuments ?? [];

    public IReadOnlyCollection<CustomDocumentRequirement> CustomOptional => CustomOptionalDocuments ?? [];

    public IReadOnlyCollection<string> CustomReceived => CustomReceivedKeys ?? [];
}
