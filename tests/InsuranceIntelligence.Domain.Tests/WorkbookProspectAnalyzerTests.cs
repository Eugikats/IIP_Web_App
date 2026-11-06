using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Workspace;
using InsuranceIntelligence.Domain.Acquisition;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class WorkbookProspectAnalyzerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] Headers =
        ["Customer ID", "First Name", "Last Name", "Phone", "Occupation", "Loan Type", "WhatsApp Opt-in", "National ID"];

    private static IReadOnlyList<string> Row(params string[] cells) => cells;

    private static readonly KatoBrand Bank = new(
        "Test Bank", "Test Bank",
        [MicroinsuranceCatalog.NileShield, MicroinsuranceCatalog.Mpingo, MicroinsuranceCatalog.Kifaru]);

    private static WorkbookAnalysis Analyse(IReadOnlyList<string>[] rows, string[]? headers = null, KatoBrand? brand = null) =>
        WorkbookProspectAnalyzer.Analyse("customers.xlsx", headers ?? Headers, rows, truncated: false, Now, brand ?? KatoBrand.NileShield);

    [Theory]
    [InlineData("0776678989", "+256776678989")]
    [InlineData("776678989", "+256776678989")]
    [InlineData("+256776678989", "+256776678989")]
    [InlineData("256 776 678 989", "+256776678989")]
    [InlineData("00256776678989", "+256776678989")]
    [InlineData("2.56776678989E+11", "+256776678989")]
    [InlineData("+254712345678", "+254712345678")]
    public void Phone_numbers_are_normalised_to_international_form(string raw, string expected)
    {
        var (phone, problem) = WorkbookProspectAnalyzer.NormalisePhone(raw);
        Assert.Equal(expected, phone);
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0612345678")]
    [InlineData("256612345678")]
    [InlineData("0776 6789")]
    public void Unusable_phone_numbers_are_rejected_not_guessed(string raw)
    {
        var (phone, problem) = WorkbookProspectAnalyzer.NormalisePhone(raw);
        Assert.Null(phone);
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void A_consented_boda_rider_is_recommended_the_rider_cover_with_reasons_from_the_file()
    {
        var analysis = Analyse([Row("C1", "Jonathan", "Okello", "0776678989", "Boda boda rider", "", "Yes", "CM1234567")]);

        var prospect = Assert.Single(analysis.Prospects);
        Assert.True(prospect.Contactable);
        Assert.Equal(MicroinsuranceCatalog.BodaAccidentLine, prospect.ProductLine);
        Assert.Contains(prospect.Reasons, reason => reason.Contains("boda", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Ready for campaign review", prospect.ReviewStatus);
        Assert.StartsWith("Hello Jonathan,", prospect.MessagePreview);
        Assert.DoesNotContain("{{", prospect.MessagePreview);
        Assert.Equal(1, analysis.Summary.Eligible);
    }

    [Fact]
    public void Missing_consent_duplicates_invalid_numbers_and_opt_outs_are_excluded_with_a_reason()
    {
        var analysis = Analyse(
        [
            Row("C1", "Ann", "A", "0776678989", "Trader", "", "Yes", ""),
            Row("C2", "Ben", "B", "0782000111", "Trader", "", "", ""),
            Row("C3", "Ann", "A", "+256776678989", "Trader", "", "Yes", ""),
            Row("C4", "Cal", "C", "12345", "Trader", "", "Yes", ""),
            Row("C5", "Dee", "D", "0701000222", "Trader", "", "No", ""),
        ]);

        Assert.Equal(1, analysis.Summary.Eligible);
        Assert.Equal(5, analysis.Summary.TotalRecords);
        Assert.DoesNotContain(analysis.Prospects, p => p.CustomerRef == "C3");
        Assert.Contains(analysis.Exclusions, e => e.CustomerRef == "C2" && e.Reason == "No messaging consent recorded");
        Assert.Contains(analysis.Exclusions, e => e.CustomerRef == "C3" && e.Reason.StartsWith("Duplicate"));
        Assert.Contains(analysis.Exclusions, e => e.CustomerRef == "C4" && e.Reason.StartsWith("Invalid number"));
        Assert.Contains(analysis.Exclusions, e => e.CustomerRef == "C5" && e.Reason == "Opted out of messaging");
        Assert.All(analysis.Prospects.Where(p => !p.Contactable), p => Assert.Equal("Excluded from campaign", p.ReviewStatus));
    }

    [Fact]
    public void Sensitive_columns_are_listed_and_never_read()
    {
        var analysis = Analyse([Row("C1", "Ann", "A", "0776678989", "Trader", "", "Yes", "CM1234567")]);

        Assert.Contains("National ID", analysis.SensitiveColumnsIgnored);
        Assert.DoesNotContain(analysis.Mappings, m => m.Header == "National ID");
        Assert.DoesNotContain("CM1234567", string.Join(' ', analysis.Prospects.SelectMany(p => p.Reasons.Concat(p.Missing))));
    }

    [Fact]
    public void An_opt_out_column_overrides_a_consent_flag()
    {
        Assert.Equal(ContactConsentStatus.Withdrawn, WorkbookProspectAnalyzer.ParseConsent("Yes", "Yes"));
        Assert.Equal(ContactConsentStatus.Agreed, WorkbookProspectAnalyzer.ParseConsent("Yes", "No"));
        Assert.Equal(ContactConsentStatus.NotRecorded, WorkbookProspectAnalyzer.ParseConsent("maybe", ""));
    }

    [Fact]
    public void Stated_interest_in_an_unconfigured_cover_is_reported_and_sent_for_human_review()
    {
        string[] headers = ["Name", "Phone", "Consent", "Insurance Interest"];
        var analysis = Analyse([Row("Ann Nabirye", "0776678989", "Yes", "Motor comprehensive")], headers);

        var prospect = Assert.Single(analysis.Prospects);
        Assert.Contains(prospect.Missing, item => item.Contains("motor", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Needs human review", prospect.ReviewStatus);
        Assert.Equal("No clear match", prospect.SuggestedProduct);
        Assert.Contains("explore insurance cover", prospect.MessagePreview);
    }

    [Fact]
    public void A_person_with_nothing_recorded_gets_the_generic_message_not_an_invented_cover()
    {
        string[] headers = ["Name", "Phone", "Consent"];
        var analysis = Analyse([Row("Ann Nabirye", "0776678989", "Yes")], headers);

        var prospect = Assert.Single(analysis.Prospects);
        Assert.Equal("No clear match", prospect.SuggestedProduct);
        Assert.Equal("Ready for campaign review", prospect.ReviewStatus);
        Assert.Contains("explore insurance cover", prospect.MessagePreview);
    }

    [Fact]
    public void Stated_interest_in_a_configured_cover_raises_the_score_and_names_it()
    {
        string[] headers = ["Name", "Phone", "Consent", "Insurance Interest"];
        var analysis = Analyse([Row("Ann Nabirye", "0776678989", "Yes", "Funeral, life")], headers, Bank);

        var prospect = Assert.Single(analysis.Prospects);
        Assert.Equal(MicroinsuranceCatalog.FuneralLine, prospect.ProductLine);

        var without = Analyse([Row("Ann Nabirye", "0776678989", "Yes")], ["Name", "Phone", "Consent"], Bank);
        Assert.True(
            prospect.Score > without.Prospects[0].Score,
            $"stated interest should raise the score: {prospect.Score} vs {without.Prospects[0].Score}");
        Assert.Contains(prospect.Reasons, reason => reason.Contains("Asked about it"));
    }

    [Fact]
    public void A_missing_name_gets_a_neutral_greeting_and_no_invented_name()
    {
        string[] headers = ["Phone", "Consent"];
        var analysis = Analyse([Row("0776678989", "Yes")], headers);

        var prospect = Assert.Single(analysis.Prospects);
        Assert.Equal("Name not provided", prospect.Name);
        Assert.StartsWith("Hello, thanks", prospect.MessagePreview);
    }

    [Fact]
    public void Contact_numbers_are_masked_in_the_result_and_every_step_reports_a_real_outcome()
    {
        var analysis = Analyse([Row("C1", "Ann", "A", "0776678989", "Trader", "", "Yes", "")]);

        Assert.Equal("+256 *** ***989", analysis.Prospects[0].ContactMasked);
        Assert.Equal(9, analysis.Steps.Count);
        Assert.All(analysis.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.Detail)));
        Assert.Contains(analysis.Steps, step => step.Label == "Validating templates" && step.Status == "Warning");
    }

    [Fact]
    public void The_same_file_always_gives_the_same_scores()
    {
        var rows = new[] { Row("C1", "Ann", "A", "0776678989", "Boda rider", "Vehicle loan", "Yes", "") };
        var first = Analyse(rows);
        var second = Analyse(rows);

        Assert.Equal(first.Prospects[0].Score, second.Prospects[0].Score);
        Assert.Equal(first.Prospects[0].Reasons, second.Prospects[0].Reasons);
    }
}
