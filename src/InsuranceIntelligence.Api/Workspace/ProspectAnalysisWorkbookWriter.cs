using ClosedXML.Excel;
using InsuranceIntelligence.Application.Workspace;

namespace InsuranceIntelligence.Api.Workspace;

/// <summary>
/// Writes an analysis to an .xlsx workbook with four sheets: the prospect analysis, the campaign
/// recommendations, the exclusions and a summary. The summary names the analysis id, so an exported file
/// can always be traced back to the run that produced it. Contact numbers are masked and no source
/// column the analysis did not need is included.
/// </summary>
public static class ProspectAnalysisWorkbookWriter
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#0B1640");

    public static byte[] Write(WorkbookAnalysis analysis)
    {
        using var workbook = new XLWorkbook();
        WriteProspects(workbook.AddWorksheet("Prospect Analysis"), analysis);
        WriteCampaign(workbook.AddWorksheet("Campaign Recommendations"), analysis);
        WriteExclusions(workbook.AddWorksheet("Exclusions"), analysis);
        WriteSummary(workbook.AddWorksheet("Summary"), analysis);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteProspects(IXLWorksheet sheet, WorkbookAnalysis analysis)
    {
        string[] headers =
        [
            "Customer ID", "Customer name", "Contact (masked)", "Suggested product", "Insurer", "Opportunity score",
            "Band", "Score explanation", "Missing information", "Recommended next action", "Review status", "Consent as stated in file",
        ];
        Header(sheet, headers);

        var row = 2;
        foreach (var p in analysis.Prospects)
        {
            sheet.Cell(row, 1).Value = p.CustomerRef;
            sheet.Cell(row, 2).Value = p.Name;
            sheet.Cell(row, 3).Value = p.ContactMasked;
            sheet.Cell(row, 4).Value = p.SuggestedProduct;
            sheet.Cell(row, 5).Value = p.Insurer;
            sheet.Cell(row, 6).Value = p.Score;
            sheet.Cell(row, 7).Value = p.Band;
            sheet.Cell(row, 8).Value = string.Join("; ", p.Reasons);
            sheet.Cell(row, 9).Value = string.Join("; ", p.Missing);
            sheet.Cell(row, 10).Value = p.NextAction;
            sheet.Cell(row, 11).Value = p.ReviewStatus;
            sheet.Cell(row, 12).Value = p.Consent;
            row++;
        }

        Finish(sheet, headers.Length, row - 1, [14, 24, 20, 28, 22, 12, 16, 60, 40, 50, 26, 30]);
        sheet.Column(8).Style.Alignment.WrapText = true;
        sheet.Column(10).Style.Alignment.WrapText = true;
    }

    private static void WriteCampaign(IXLWorksheet sheet, WorkbookAnalysis analysis)
    {
        string[] headers =
        [
            "Customer ID", "Suggested campaign", "Template name", "Template approval", "Prepared message preview",
            "Messaging eligibility", "Approval status",
        ];
        Header(sheet, headers);

        var row = 2;
        foreach (var p in analysis.Prospects)
        {
            sheet.Cell(row, 1).Value = p.CustomerRef;
            sheet.Cell(row, 2).Value = p.ProductLine.Length == 0 ? "Generic product discovery" : p.SuggestedProduct;
            sheet.Cell(row, 3).Value = p.TemplateName;
            sheet.Cell(row, 4).Value = "Not verified";
            sheet.Cell(row, 5).Value = p.MessagePreview;
            sheet.Cell(row, 6).Value = p.Contactable ? "Eligible to review" : "Excluded";
            sheet.Cell(row, 7).Value = "Not approved";
            row++;
        }

        Finish(sheet, headers.Length, row - 1, [14, 30, 32, 18, 90, 20, 16]);
        sheet.Column(5).Style.Alignment.WrapText = true;
    }

    private static void WriteExclusions(IXLWorksheet sheet, WorkbookAnalysis analysis)
    {
        string[] headers = ["Customer ID", "Row in file", "Exclusion reason", "Required remediation"];
        Header(sheet, headers);

        var row = 2;
        foreach (var e in analysis.Exclusions.OrderBy(e => e.RowNumber))
        {
            sheet.Cell(row, 1).Value = e.CustomerRef;
            sheet.Cell(row, 2).Value = e.RowNumber;
            sheet.Cell(row, 3).Value = e.Reason;
            sheet.Cell(row, 4).Value = e.Remediation;
            row++;
        }

        Finish(sheet, headers.Length, row - 1, [14, 12, 36, 70]);
    }

    private static void WriteSummary(IXLWorksheet sheet, WorkbookAnalysis analysis)
    {
        var s = analysis.Summary;
        var row = 1;

        void Pair(string label, object value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = XLCellValue.FromObject(value);
            row++;
        }

        void Section(string title)
        {
            row++;
            sheet.Cell(row, 1).Value = title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontColor = HeaderFill;
            row++;
        }

        Pair("Analysis ID", analysis.AnalysisId);
        Pair("Source file", analysis.FileName);
        Pair("Analysed at (UTC)", analysis.AnalysedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm"));
        Pair("Institution", analysis.BankName);

        Section("Counts");
        Pair("Total customer records", s.TotalRecords);
        Pair("Valid contact numbers", s.ValidContacts);
        Pair("Eligible prospects", s.Eligible);
        Pair("Excluded", s.Excluded);
        Pair("Customers requiring review", s.NeedsReview);
        Pair("Campaign readiness", s.CampaignReadiness);

        Section("Score distribution");
        foreach (var item in s.ScoreDistribution) Pair(item.Label, item.Count);

        Section("Product recommendations");
        foreach (var item in s.ProductRecommendations) Pair(item.Label, item.Count);

        Section("Exclusion reasons");
        foreach (var item in s.ExclusionReasons) Pair(item.Label, item.Count);

        Section("Notes");
        foreach (var note in analysis.Notices)
        {
            sheet.Cell(row, 1).Value = note;
            row++;
        }

        sheet.Column(1).Width = 36;
        sheet.Column(2).Width = 60;
    }

    private static void Header(IXLWorksheet sheet, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = HeaderFill;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        sheet.Row(1).Height = 22;
    }

    private static void Finish(IXLWorksheet sheet, int columns, int lastRow, double[] widths)
    {
        for (var i = 0; i < widths.Length; i++) sheet.Column(i + 1).Width = widths[i];
        sheet.SheetView.FreezeRows(1);
        if (lastRow >= 1)
        {
            sheet.Range(1, 1, Math.Max(lastRow, 1), columns).SetAutoFilter();
        }

        sheet.Range(1, 1, Math.Max(lastRow, 1), columns).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.Row(1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }
}
