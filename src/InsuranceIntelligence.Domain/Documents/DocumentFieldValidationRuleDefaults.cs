using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// The built-in rule set every tenant gets before adding any of their own — deliberately kept to
/// facts that are independently verifiable, not assumed. A Ugandan National Identification Number
/// (NIN) is defined as 14 characters by the National Identification and Registration Authority
/// (NIRA); that is the one rule seeded here.
///
/// Rules requiring a specific format for a driving permit number or a police abstract/OB reference
/// are deliberately NOT included — no verified source for those formats was available while
/// building this, and guessing one risks rejecting genuinely valid documents (A1: never invent an
/// unsupported domain fact). The framework supports adding them the moment someone with real
/// regulatory knowledge supplies the correct pattern; see DocumentFieldValidationRule and
/// IDocumentFieldValidationRuleRepository for how a tenant adds its own without a deployment.
/// </summary>
public static class DocumentFieldValidationRuleDefaults
{
    public const string NationalIdRuleId = "default.national-id-14-chars";

    public static IReadOnlyCollection<DocumentFieldValidationRule> All(TenantScope scope) =>
    [
        new DocumentFieldValidationRule
        {
            RuleId = NationalIdRuleId,
            Scope = scope,
            DocumentType = DocumentType.CustomerId,
            FieldLabel = "National ID number (NIN)",
            FieldKeyAliases = ["national_id", "nin", "id_number", "identification_number", "id_no"],
            Kind = DocumentFieldRuleKind.ExactLength,
            ExactLength = 14,
            Severity = "warning",
            Message = "A Ugandan National ID number (NIN) is 14 characters. The extracted value does not match — verify the document and correct the field if needed.",
            ApplicableProductLines = [],
            Source = "NIRA — Uganda National Identification Number format",
        },
    ];
}
