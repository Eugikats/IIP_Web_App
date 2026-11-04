using System.Text;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Compliance;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 24: the small text PDF writer behind the claim summary.</summary>
public sealed class SimplePdfTests
{
    private static string Text(byte[] pdf) => Encoding.ASCII.GetString(pdf);

    private static SimplePdfDocument Sample()
    {
        var pdf = new SimplePdfDocument("Claim CLM-1  |  footer");
        pdf.Title("Claim summary CLM-1");
        pdf.Heading("The claim");
        pdf.Row("Status", "Under review");
        pdf.Paragraph("A short paragraph.");
        pdf.Bullet("A bullet");
        return pdf;
    }

    [Fact]
    public void ItWritesAWellFormedFileWhoseIndexPointsAtEveryObject()
    {
        var bytes = Sample().ToBytes();
        var text = Text(bytes);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        var startXref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n").Groups[1].Value);
        Assert.StartsWith("xref", text[startXref..]);
        var offsets = Regex.Matches(text[startXref..], @"(\d{10}) 00000 n").Select(match => int.Parse(match.Groups[1].Value)).ToArray();
        Assert.True(offsets.Length >= 6);
        for (var index = 0; index < offsets.Length; index++)
        {
            Assert.StartsWith($"{index + 1} 0 obj", text[offsets[index]..]);
        }

        Assert.Contains($"/Size {offsets.Length + 1}", text);
    }

    [Fact]
    public void TheTextIsThereToBeSelectedAndEachPageHasItsFooter()
    {
        var text = Text(Sample().ToBytes());

        Assert.Contains("(Claim summary CLM-1)", text);
        Assert.Contains("(Status: Under review)", text);
        Assert.Contains("(- A bullet)", text);
        Assert.Contains("(Claim CLM-1  |  footer  |  Page 1 of 1)", text);
        Assert.Contains("/BaseFont /Helvetica-Bold", text);
    }

    [Fact]
    public void LongContentRunsOnToMoreThanOnePageAndTheFootersCountThem()
    {
        var pdf = new SimplePdfDocument("Footer");
        for (var index = 0; index < 200; index++) pdf.Bullet($"Line number {index} of a long list that needs more than one page to hold it all.");

        var text = Text(pdf.ToBytes());
        var pages = int.Parse(Regex.Match(text, @"/Type /Pages /Count (\d+)").Groups[1].Value);

        Assert.True(pages >= 3);
        Assert.Equal(pages, Regex.Matches(text, @"/Type /Page /Parent").Count);
        Assert.Contains($"Page {pages} of {pages}", text);
        Assert.Contains("Page 1 of " + pages, text);
    }

    [Fact]
    public void NoLineIsWiderThanTheTextArea()
    {
        var pdf = new SimplePdfDocument("Footer");
        pdf.Paragraph(string.Join(' ', Enumerable.Repeat("wide words that must wrap rather than run off the side of the page", 12)));
        pdf.Paragraph(new string('W', 300));

        var text = Text(pdf.ToBytes());

        foreach (Match match in Regex.Matches(text, @"BT /F1 10 Tf [\d.]+ [\d.]+ Td \((.*?)\) Tj ET"))
        {
            Assert.True(SimplePdfDocument.Measure(match.Groups[1].Value, bold: false, size: 10) <= 595 - 100 + 0.5, "line fits within the margins");
        }
    }

    [Fact]
    public void BracketsBackslashesAccentsAndDashesAreSafe()
    {
        var pdf = new SimplePdfDocument("Footer");
        pdf.Paragraph("Cost (UGX) a\\b caf\u00e9 \u2014 done \u201cquoted\u201d \u4e2d");

        var text = Text(pdf.ToBytes());

        Assert.Contains(@"Cost \(UGX\) a\\b cafe - done ""quoted"" ?", text);
        Assert.DoesNotContain('\u2014', text);
    }

    [Fact]
    public void AnEmptyDocumentIsStillAValidOnePagePdf()
    {
        var text = Text(new SimplePdfDocument("Footer").ToBytes());

        Assert.Contains("/Count 1", text);
        Assert.Contains("Page 1 of 1", text);
        Assert.StartsWith("%PDF-1.4", text);
    }

    [Fact]
    public void WiderLettersMeasureWiderAndBoldIsWiderThanRegular()
    {
        Assert.True(SimplePdfDocument.Measure("WWWW", false, 10) > SimplePdfDocument.Measure("iiii", false, 10));
        Assert.True(SimplePdfDocument.Measure("Claim", true, 10) > SimplePdfDocument.Measure("Claim", false, 10));
        Assert.Equal(0, SimplePdfDocument.Measure(string.Empty, false, 10));
    }
}
