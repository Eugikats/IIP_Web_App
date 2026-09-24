using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Domain.Tests.Policies;

public sealed class PolicyImportRowValidatorTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidRow_ProducesAFileImportSourcedPolicy()
    {
        var row = new Dictionary<string, string>
        {
            [PolicyImportFields.PolicyNumber] = "POL-1001",
            [PolicyImportFields.ProductLine] = "Motor",
            [PolicyImportFields.EffectiveFrom] = "2026-01-01",
            [PolicyImportFields.EffectiveTo] = "2026-12-31",
            [PolicyImportFields.AnnualPremium] = "1,200,000",
        };

        var outcome = PolicyImportRowValidator.Validate(Scope, 2, row, "policy-guid-1", Now);

        Assert.True(outcome.IsValid);
        Assert.NotNull(outcome.Policy);
        Assert.Equal("POL-1001", outcome.Policy!.PolicyNumber);
        Assert.Equal(PolicyRecordSource.FileImport, outcome.Policy.RecordSource);
        Assert.Equal(Now, outcome.Policy.LastRefreshedAt);
        Assert.Equal(1_200_000m, outcome.Policy.AnnualPremium);
        Assert.Equal("policy-guid-1", outcome.Policy.PolicyId);
    }

    [Fact]
    public void MissingPolicyNumber_Fails()
    {
        var row = new Dictionary<string, string> { [PolicyImportFields.ProductLine] = "Motor" };

        var outcome = PolicyImportRowValidator.Validate(Scope, 5, row, "policy-guid-2", Now);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, error => error.Contains("Policy number", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(5, outcome.RowNumber);
    }

    [Fact]
    public void UnparseableDate_FailsWithARowSpecificMessage()
    {
        var row = new Dictionary<string, string>
        {
            [PolicyImportFields.PolicyNumber] = "POL-1002",
            [PolicyImportFields.ProductLine] = "Motor",
            [PolicyImportFields.EffectiveFrom] = "not-a-date",
        };

        var outcome = PolicyImportRowValidator.Validate(Scope, 3, row, "policy-guid-3", Now);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, error => error.Contains("not-a-date", StringComparison.Ordinal));
    }

    [Fact]
    public void CoverEndBeforeCoverStart_Fails()
    {
        var row = new Dictionary<string, string>
        {
            [PolicyImportFields.PolicyNumber] = "POL-1003",
            [PolicyImportFields.ProductLine] = "Motor",
            [PolicyImportFields.EffectiveFrom] = "2026-06-01",
            [PolicyImportFields.EffectiveTo] = "2026-01-01",
        };

        var outcome = PolicyImportRowValidator.Validate(Scope, 4, row, "policy-guid-4", Now);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, error => error.Contains("before", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SuggestFieldFor_MatchesCommonHeaderVariants()
    {
        Assert.Equal(PolicyImportFields.PolicyNumber, PolicyImportFields.SuggestFieldFor("Policy Number"));
        Assert.Equal(PolicyImportFields.PolicyNumber, PolicyImportFields.SuggestFieldFor("policyNumber"));
        Assert.Null(PolicyImportFields.SuggestFieldFor("Unrelated Column"));
    }
}
