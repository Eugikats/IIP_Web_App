using InsuranceIntelligence.Application.Exports;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>The two ways data crosses between an insurer's own systems and IIP without any custom
/// connector: policies in from the export their core system produces, claims out as a register.</summary>
public sealed class IntegrationReadinessTests
{
    [Theory]
    // Typical headers from core-system and spreadsheet exports, in whatever case and punctuation.
    [InlineData("POLICY_NO", PolicyImportFields.PolicyNumber)]
    [InlineData("Pol. No", PolicyImportFields.PolicyNumber)]
    [InlineData("Policy Ref", PolicyImportFields.PolicyNumber)]
    [InlineData("Certificate No", PolicyImportFields.PolicyNumber)]
    [InlineData("Class of Business", PolicyImportFields.ProductLine)]
    [InlineData("LOB", PolicyImportFields.ProductLine)]
    [InlineData("Sub-Class", PolicyImportFields.ProductLine)]
    [InlineData("Cover", PolicyImportFields.CoverType)]
    [InlineData("Client Code", PolicyImportFields.CustomerId)]
    [InlineData("Insured ID", PolicyImportFields.CustomerId)]
    [InlineData("Branch Code", PolicyImportFields.BranchId)]
    [InlineData("Inception Date", PolicyImportFields.EffectiveFrom)]
    [InlineData("Start Date", PolicyImportFields.EffectiveFrom)]
    [InlineData("Expiry Date", PolicyImportFields.EffectiveTo)]
    [InlineData("Renewal Date", PolicyImportFields.EffectiveTo)]
    [InlineData("Gross Premium", PolicyImportFields.AnnualPremium)]
    [InlineData("GWP", PolicyImportFields.AnnualPremium)]
    [InlineData("CCY", PolicyImportFields.PremiumCurrency)]
    [InlineData("Sum Assured", PolicyImportFields.SumInsured)]
    [InlineData("TSI", PolicyImportFields.SumInsured)]
    [InlineData("policyNumber", PolicyImportFields.PolicyNumber)] // the platform's own names still work
    [InlineData("Annual Premium", PolicyImportFields.AnnualPremium)]
    public void AnExportsOwnColumnNames_AreMatchedWithoutAnyMapping(string header, string expected) =>
        Assert.Equal(expected, PolicyImportFields.SuggestFieldFor(header));

    [Theory]
    [InlineData("Remarks")]
    [InlineData("Created By")]
    [InlineData("")]
    public void AColumnNobodyRecognises_IsLeftForThePersonToChoose(string header) =>
        Assert.Null(PolicyImportFields.SuggestFieldFor(header));

    [Fact]
    public void TheClaimsRegister_HasTheDocumentedColumns_InOrder_AndOneRowPerClaim()
    {
        var csv = ClaimsRegisterExporter.BuildCsv(
            [Claim("POL-1", new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero))], new ClaimsRegisterFilter(null, null, null));
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(string.Join(',', ClaimsRegisterExporter.Columns), lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.Contains("POL-1", lines[1]);
        Assert.Contains("2026-03-01", lines[1]);
        Assert.Equal(ClaimsRegisterExporter.Columns.Count, lines[1].Split(',').Length);
    }

    [Fact]
    public void TheRegister_CanBeLimitedByReportedDate_AndStatus()
    {
        var march = Claim("POL-MARCH", new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero));
        var april = Claim("POL-APRIL", new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero)) with { Status = CaseStatus.Settled };
        var all = new[] { march, april };

        var onlyMarch = ClaimsRegisterExporter.Select(all, new ClaimsRegisterFilter(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), null));
        var onlySettled = ClaimsRegisterExporter.Select(all, new ClaimsRegisterFilter(null, null, "settled"));

        Assert.Equal("POL-MARCH", Assert.Single(onlyMarch).PolicyNumber);
        Assert.Equal("POL-APRIL", Assert.Single(onlySettled).PolicyNumber);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("Okello, John", "\"Okello, John\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(\"http://evil.example\")", "\"'=HYPERLINK(\"\"http://evil.example\"\")\"")]
    [InlineData("+256700000000", "'+256700000000")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public void ACellIsQuotedWhenNeeded_AndAFormulaIsMadeInert(string value, string expected) =>
        Assert.Equal(expected, ClaimsRegisterExporter.Cell(value));

    private static ClaimCase Claim(string policy, DateTimeOffset reported) => new()
    {
        CaseId = Guid.NewGuid(),
        Scope = TenantScope.Create("org-acme", "main"),
        CustomerName = "Amina Okello",
        PolicyNumber = policy,
        ProductLine = "Motor",
        ClaimType = "Accident",
        Channel = "WhatsApp",
        IncidentDate = DateOnly.FromDateTime(reported.UtcDateTime).AddDays(-1),
        IncidentDescription = "Collision",
        Status = CaseStatus.New,
        CompletenessScore = 0,
        CreatedByUserId = "officer",
        CreatedAt = reported,
        UpdatedAt = reported,
        CorrelationId = "c",
    };
}
