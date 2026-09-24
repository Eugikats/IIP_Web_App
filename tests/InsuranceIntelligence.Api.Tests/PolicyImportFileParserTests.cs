using System.Text;
using ClosedXML.Excel;
using InsuranceIntelligence.Api.Policies;

namespace InsuranceIntelligence.Api.Tests;

public sealed class PolicyImportFileParserTests
{
    [Fact]
    public void Parse_Csv_SplitsHeadersAndRows()
    {
        var csv = "Policy Number,Product\nPOL-1,Motor\nPOL-2,Marine\n";
        using var stream = ToStream(csv);

        var parsed = PolicyImportFileParser.Parse("policies.csv", stream);

        Assert.Equal(["Policy Number", "Product"], parsed.Headers);
        Assert.Equal(2, parsed.Rows.Count);
        Assert.Equal(["POL-1", "Motor"], parsed.Rows[0]);
        Assert.Equal(["POL-2", "Marine"], parsed.Rows[1]);
        Assert.False(parsed.Truncated);
    }

    [Fact]
    public void Parse_Csv_HandlesQuotedFieldsWithEmbeddedCommasAndEscapedQuotes()
    {
        var csv = "Policy Number,Notes\nPOL-1,\"Acme, Inc. said \"\"pay now\"\"\"\n";
        using var stream = ToStream(csv);

        var parsed = PolicyImportFileParser.Parse("policies.csv", stream);

        Assert.Single(parsed.Rows);
        Assert.Equal("Acme, Inc. said \"pay now\"", parsed.Rows[0][1]);
    }

    [Fact]
    public void Parse_Csv_HandlesCrlfAndLfLineEndings()
    {
        var csv = "Policy Number,Product\r\nPOL-1,Motor\nPOL-2,Marine\r\n";
        using var stream = ToStream(csv);

        var parsed = PolicyImportFileParser.Parse("policies.csv", stream);

        Assert.Equal(2, parsed.Rows.Count);
    }

    [Fact]
    public void Parse_UnsupportedExtension_Throws()
    {
        using var stream = ToStream("irrelevant");

        Assert.Throws<UnsupportedImportFileException>(() => PolicyImportFileParser.Parse("policies.pdf", stream));
    }

    [Fact]
    public void Parse_Xlsx_ReadsHeadersAndRowsFromTheFirstWorksheet()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Policies");
        sheet.Cell(1, 1).Value = "Policy Number";
        sheet.Cell(1, 2).Value = "Product";
        sheet.Cell(2, 1).Value = "POL-9001";
        sheet.Cell(2, 2).Value = "Motor";
        sheet.Cell(3, 1).Value = "POL-9002";
        sheet.Cell(3, 2).Value = "Marine";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var parsed = PolicyImportFileParser.Parse("policies.xlsx", stream);

        Assert.Equal(["Policy Number", "Product"], parsed.Headers);
        Assert.Equal(2, parsed.Rows.Count);
        Assert.Equal(["POL-9001", "Motor"], parsed.Rows[0]);
        Assert.Equal(["POL-9002", "Marine"], parsed.Rows[1]);
    }

    private static MemoryStream ToStream(string content) => new(Encoding.UTF8.GetBytes(content));
}
