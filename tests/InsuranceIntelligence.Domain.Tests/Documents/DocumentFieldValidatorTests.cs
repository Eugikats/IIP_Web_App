using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests.Documents;

public sealed class DocumentFieldValidatorTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public void EvaluateFlagsNationalIdThatIsNotFourteenCharacters()
    {
        var rules = DocumentFieldValidator.ResolveRules(Scope, tenantRules: []);
        var extractedFields = new Dictionary<string, string> { ["field.national_id"] = "CM12345" };

        var warnings = DocumentFieldValidator.Evaluate(rules, DocumentType.CustomerId, "Motor", extractedFields);

        var warning = Assert.Single(warnings);
        Assert.Equal("National ID number (NIN)", warning.FieldName);
        Assert.Equal("CM12345", warning.ObservedValue);
        Assert.Equal("14 characters", warning.ExpectedValue);
    }

    [Fact]
    public void EvaluateAcceptsNationalIdThatIsFourteenCharacters()
    {
        var rules = DocumentFieldValidator.ResolveRules(Scope, tenantRules: []);
        var extractedFields = new Dictionary<string, string> { ["keyValue.NationalIdNumber"] = "CM12345678901X" };

        var warnings = DocumentFieldValidator.Evaluate(rules, DocumentType.CustomerId, "Motor", extractedFields);

        Assert.Empty(warnings);
    }

    [Fact]
    public void EvaluateMatchesAliasRegardlessOfExactExtractedKeyName()
    {
        var rules = DocumentFieldValidator.ResolveRules(Scope, tenantRules: []);
        var extractedFields = new Dictionary<string, string> { ["field.NIN No."] = "short" };

        var warnings = DocumentFieldValidator.Evaluate(rules, DocumentType.CustomerId, "Motor", extractedFields);

        Assert.Single(warnings);
    }

    [Fact]
    public void EvaluateSkipsRuleWhenFieldWasNeverExtracted()
    {
        var rules = DocumentFieldValidator.ResolveRules(Scope, tenantRules: []);
        var extractedFields = new Dictionary<string, string> { ["field.PolicyNumber"] = "NSA/MO/2026/00033" };

        var warnings = DocumentFieldValidator.Evaluate(rules, DocumentType.CustomerId, "Motor", extractedFields);

        Assert.Empty(warnings);
    }

    [Fact]
    public void EvaluateIgnoresRulesForOtherDocumentTypes()
    {
        var rules = DocumentFieldValidator.ResolveRules(Scope, tenantRules: []);
        var extractedFields = new Dictionary<string, string> { ["field.national_id"] = "CM12345" };

        var warnings = DocumentFieldValidator.Evaluate(rules, DocumentType.ClaimForm, "Motor", extractedFields);

        Assert.Empty(warnings);
    }

    [Fact]
    public void ResolveRulesLetsATenantRuleWithTheSameIdOverrideTheDefault()
    {
        var tenantOverride = new DocumentFieldValidationRule
        {
            RuleId = DocumentFieldValidationRuleDefaults.NationalIdRuleId,
            Scope = Scope,
            DocumentType = DocumentType.CustomerId,
            FieldLabel = "National ID",
            FieldKeyAliases = ["national_id", "nin"],
            Kind = DocumentFieldRuleKind.ExactLength,
            ExactLength = 8,
            Severity = "warning",
            Message = "Overridden by tenant.",
            Enabled = true
        };

        var rules = DocumentFieldValidator.ResolveRules(Scope, tenantRules: [tenantOverride]);

        var resolved = Assert.Single(rules, rule => rule.RuleId == DocumentFieldValidationRuleDefaults.NationalIdRuleId);
        Assert.Equal(8, resolved.ExactLength);
        Assert.Equal("Overridden by tenant.", resolved.Message);
    }

    [Fact]
    public void EvaluateSkipsDisabledRules()
    {
        var disabled = new DocumentFieldValidationRule
        {
            RuleId = DocumentFieldValidationRuleDefaults.NationalIdRuleId,
            Scope = Scope,
            DocumentType = DocumentType.CustomerId,
            FieldLabel = "National ID",
            FieldKeyAliases = ["national_id", "nin"],
            Kind = DocumentFieldRuleKind.ExactLength,
            ExactLength = 14,
            Severity = "warning",
            Message = "National ID must be 14 characters.",
            Enabled = false
        };
        var rules = DocumentFieldValidator.ResolveRules(Scope, tenantRules: [disabled]);
        var extractedFields = new Dictionary<string, string> { ["field.national_id"] = "CM12345" };

        var warnings = DocumentFieldValidator.Evaluate(rules, DocumentType.CustomerId, "Motor", extractedFields);

        Assert.Empty(warnings);
    }

    [Fact]
    public void EvaluateHonorsApplicableProductLinesWhenSet()
    {
        var motorOnly = new DocumentFieldValidationRule
        {
            RuleId = "test.motor-only",
            Scope = Scope,
            DocumentType = DocumentType.CustomerId,
            FieldLabel = "National ID",
            FieldKeyAliases = ["national_id"],
            Kind = DocumentFieldRuleKind.ExactLength,
            ExactLength = 14,
            Severity = "warning",
            Message = "National ID must be 14 characters.",
            ApplicableProductLines = ["Motor"]
        };
        var rules = new[] { motorOnly };
        var extractedFields = new Dictionary<string, string> { ["field.national_id"] = "short" };

        var motorWarnings = DocumentFieldValidator.Evaluate(rules, DocumentType.CustomerId, "Motor", extractedFields);
        var healthWarnings = DocumentFieldValidator.Evaluate(rules, DocumentType.CustomerId, "Health", extractedFields);

        Assert.Single(motorWarnings);
        Assert.Empty(healthWarnings);
    }
}
