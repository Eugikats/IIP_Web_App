using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// Pure evaluator: extracted field data in, real DocumentValidationWarning out. Deliberately
/// provider-agnostic, it runs once, at the point a document's classification and the owning
/// claim's product line are both known (DocumentEndpoints.UploadDocuments), rather than being
/// duplicated inside each IDocumentProcessingProvider implementation.
/// </summary>
public static class DocumentFieldValidator
{
    /// <summary>Tenant-configured rules layered over the built-in defaults, so every tenant has
    /// at least the defaults active without a seeding step, and a tenant rule with the same
    /// RuleId as a default overrides it rather than running both.</summary>
    public static IReadOnlyCollection<DocumentFieldValidationRule> ResolveRules(
        TenantScope scope, IReadOnlyCollection<DocumentFieldValidationRule> tenantRules)
    {
        var byId = DocumentFieldValidationRuleDefaults.All(scope).ToDictionary(rule => rule.RuleId);
        foreach (var rule in tenantRules)
        {
            byId[rule.RuleId] = rule;
        }
        return byId.Values.ToArray();
    }

    public static IReadOnlyCollection<DocumentValidationWarning> Evaluate(
        IReadOnlyCollection<DocumentFieldValidationRule> rules,
        DocumentType documentType,
        string productLine,
        IReadOnlyDictionary<string, string> extractedFields)
    {
        var warnings = new List<DocumentValidationWarning>();

        foreach (var rule in rules)
        {
            if (!rule.Enabled || rule.DocumentType != documentType)
            {
                continue;
            }

            if (rule.ApplicableProductLines.Count > 0
                && !rule.ApplicableProductLines.Contains(productLine, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = extractedFields.FirstOrDefault(field =>
                rule.FieldKeyAliases.Any(alias => field.Key.Contains(alias, StringComparison.OrdinalIgnoreCase)));

            // The field simply wasn't extracted at all, that is a completeness/checklist concern
            // (DocumentChecklistRules), not a shape concern. This validator only speaks to values
            // that were found and look wrong, so it never accuses a document of missing what it
            // was never asked to check for.
            if (match.Key is null)
            {
                continue;
            }

            var value = match.Value.Trim();
            var isValid = rule.Kind switch
            {
                DocumentFieldRuleKind.ExactLength => rule.ExactLength.HasValue && value.Length == rule.ExactLength.Value,
                DocumentFieldRuleKind.Pattern => !string.IsNullOrEmpty(rule.Pattern) && Regex.IsMatch(value, rule.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)),
                _ => true,
            };

            if (!isValid)
            {
                warnings.Add(new DocumentValidationWarning(
                    $"field.{rule.RuleId}",
                    rule.Message,
                    rule.Severity,
                    FieldName: rule.FieldLabel,
                    ExpectedValue: DescribeExpectation(rule),
                    ObservedValue: value));
            }
        }

        return warnings;
    }

    private static string DescribeExpectation(DocumentFieldValidationRule rule) => rule.Kind switch
    {
        DocumentFieldRuleKind.ExactLength => $"{rule.ExactLength} characters",
        DocumentFieldRuleKind.Pattern => $"matches {rule.Pattern}",
        _ => "valid",
    };
}
