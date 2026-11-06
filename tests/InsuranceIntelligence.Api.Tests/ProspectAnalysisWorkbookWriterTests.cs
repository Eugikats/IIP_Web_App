using ClosedXML.Excel;
using InsuranceIntelligence.Api.Workspace;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Workspace;
using Xunit;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ProspectAnalysisWorkbookWriterTests
{
    private static WorkbookAnalysis SampleAnalysis() =>
        WorkbookProspectAnalyzer.Analyse(
            "customers.xlsx",
            ["Customer ID", "Name", "Phone", "Occupation", "WhatsApp Opt-in"],
            [
                new[] { "C1", "Jonathan Okello", "0776678989", "Boda rider", "Yes" },
                new[] { "C2", "Ben Mugisha", "0782000111", "Trader", "" },
            ],
            truncated: false,
            new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero),
            KatoBrand.NileShield);

    [Fact]
    public void Export_has_the_four_sheets_with_headers_filters_and_one_row_per_prospect()
    {
        var analysis = SampleAnalysis();
        using var workbook = new XLWorkbook(new MemoryStream(ProspectAnalysisWorkbookWriter.Write(analysis)));

        Assert.Equal(
            ["Prospect Analysis", "Campaign Recommendations", "Exclusions", "Summary"],
            workbook.Worksheets.Select(sheet => sheet.Name).ToArray());

        var prospects = workbook.Worksheet("Prospect Analysis");
        Assert.Equal("Opportunity score", prospects.Cell(1, 6).GetString());
        Assert.Equal(analysis.Prospects.Count, prospects.RowsUsed().Count() - 1);
        Assert.True(prospects.AutoFilter.IsEnabled);
        Assert.Equal(analysis.Prospects[0].Score, prospects.Cell(2, 6).GetDouble());

        var exclusions = workbook.Worksheet("Exclusions");
        Assert.Equal(analysis.Exclusions.Count, exclusions.RowsUsed().Count() - 1);
    }

    [Fact]
    public void Export_is_traceable_to_its_analysis_and_never_carries_a_full_phone_number()
    {
        var analysis = SampleAnalysis();
        using var workbook = new XLWorkbook(new MemoryStream(ProspectAnalysisWorkbookWriter.Write(analysis)));

        var summary = workbook.Worksheet("Summary");
        Assert.Equal(analysis.AnalysisId, summary.Cell(1, 2).GetString());

        foreach (var sheet in workbook.Worksheets)
        {
            foreach (var cell in sheet.CellsUsed())
            {
                Assert.DoesNotContain("776678989", cell.GetString());
                Assert.DoesNotContain("782000111", cell.GetString());
            }
        }
    }
}
