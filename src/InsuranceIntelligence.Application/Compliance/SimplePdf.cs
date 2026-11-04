using System.Globalization;
using System.Text;

namespace InsuranceIntelligence.Application.Compliance;

/// <summary>
/// A small writer for text-only PDFs (build-loop item 24): a title, headings, paragraphs, bullets and labelled
/// rows, laid out on A4 pages with a footer. It uses the PDF standard Helvetica fonts, so no font is embedded and
/// no library or licence is involved, and the text stays selectable and searchable. It writes plain text only
/// (accents are folded to their base letter, anything else becomes a question mark).
/// </summary>
public sealed class SimplePdfDocument(string footerText)
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 50;
    private const double Top = PageHeight - 56;
    private const double Bottom = 60;

    // Helvetica glyph widths in thousandths of an em for ASCII 32 to 126 (from the standard font metrics).
    private static readonly int[] Widths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private enum Style { Title, Heading, Body, Small, BodyBold }

    private sealed record Line(string Text, Style Style, double Indent, double SpaceBefore);

    private readonly List<Line> lines = [];

    public void Title(string text) => Add(text, Style.Title, 0, 0);

    public void Heading(string text) => Add(text, Style.Heading, 0, 14);

    public void Paragraph(string text, bool small = false) => Add(text, small ? Style.Small : Style.Body, 0, 3);

    public void Bullet(string text) => Add("- " + text, Style.Body, 10, 1);

    public void Row(string label, string value) =>
        Add(label.EndsWith('?') || label.EndsWith(':') ? $"{label} {value}" : $"{label}: {value}", Style.Body, 0, 1);

    public void Strong(string text) => Add(text, Style.BodyBold, 0, 4);

    public void Space() => lines.Add(new Line(string.Empty, Style.Body, 0, 6));

    private void Add(string text, Style style, double indent, double spaceBefore)
    {
        var first = true;
        foreach (var wrapped in Wrap(Fold(text), style, PageWidth - 2 * Margin - indent))
        {
            lines.Add(new Line(wrapped, style, indent, first ? spaceBefore : 0));
            first = false;
        }
    }

    private static double Size(Style style) => style switch { Style.Title => 18, Style.Heading => 12.5, Style.Small => 8, _ => 10 };

    private static bool IsBold(Style style) => style is Style.Title or Style.Heading or Style.BodyBold;

    /// <summary>The width of text in points at the style's size.</summary>
    public static double Measure(string text, bool bold, double size)
    {
        double total = 0;
        foreach (var character in text)
        {
            var index = character - 32;
            total += index >= 0 && index < Widths.Length ? Widths[index] : 556;
        }

        return total / 1000 * size * (bold ? 1.06 : 1.0);
    }

    private static IEnumerable<string> Wrap(string text, Style style, double width)
    {
        var size = Size(style);
        var bold = IsBold(style);
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            var current = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var piece = word;
                // A single word longer than the line is cut so it cannot run off the page.
                while (Measure(piece, bold, size) > width)
                {
                    var cut = piece.Length;
                    while (cut > 1 && Measure(piece[..cut], bold, size) > width) cut--;
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }

                    yield return piece[..cut];
                    piece = piece[cut..];
                }

                var candidate = current.Length == 0 ? piece : current + " " + piece;
                if (Measure(candidate, bold, size) <= width)
                {
                    current.Clear();
                    current.Append(candidate);
                }
                else
                {
                    yield return current.ToString();
                    current.Clear();
                    current.Append(piece);
                }
            }

            if (current.Length > 0) yield return current.ToString();
        }
    }

    /// <summary>Plain ASCII: accents are folded to their letter, typographic dashes and quotes to plain ones, anything else to a question mark.</summary>
    public static string Fold(string text)
    {
        var decomposed = (text ?? string.Empty).Replace('\u2018', '\'').Replace('\u2019', '\'').Replace('\u201c', '"').Replace('\u201d', '"').Replace('\u2013', '-').Replace('\u2014', '-').Replace('\u2026', '.').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character is >= ' ' and <= '~' or '\n' or '\r' ? character : character == '\t' ? ' ' : '?');
        }

        return builder.ToString();
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    public byte[] ToBytes()
    {
        // Lay the lines out on pages.
        var pages = new List<List<(Line Line, double Y)>> { new() };
        var y = Top;
        foreach (var line in lines)
        {
            var size = Size(line.Style);
            var height = size * 1.35;
            var need = line.SpaceBefore + height;
            if (y - need < Bottom && pages[^1].Count > 0)
            {
                pages.Add(new List<(Line, double)>());
                y = Top;
            }

            y -= line.SpaceBefore + height;
            pages[^1].Add((line, y));
        }

        using var output = new MemoryStream();
        var offsets = new List<long>();
        void Write(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        void BeginObject(int number)
        {
            offsets.Add(output.Position);
            Write($"{number} 0 obj\n");
        }

        Write("%PDF-1.4\n");
        // Objects: 1 catalog, 2 page tree, 3 regular font, 4 bold font, then a content stream and a page per page.
        var firstPageObject = 5;
        var pageObjectNumbers = Enumerable.Range(0, pages.Count).Select(index => firstPageObject + index * 2 + 1).ToArray();

        BeginObject(1);
        Write("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        BeginObject(2);
        Write($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>\nendobj\n");
        BeginObject(3);
        Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        BeginObject(4);
        Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        for (var index = 0; index < pages.Count; index++)
        {
            var content = new StringBuilder();
            foreach (var (line, lineY) in pages[index])
            {
                if (line.Text.Length == 0) continue;
                content.Append($"BT /{(IsBold(line.Style) ? "F2" : "F1")} {Size(line.Style).ToString("0.##", CultureInfo.InvariantCulture)} Tf {(Margin + line.Indent).ToString("0.##", CultureInfo.InvariantCulture)} {lineY.ToString("0.##", CultureInfo.InvariantCulture)} Td ({Escape(line.Text)}) Tj ET\n");
            }

            var footer = Fold($"{footerText}  |  Page {index + 1} of {pages.Count}");
            content.Append($"BT /F1 8 Tf {Margin.ToString(CultureInfo.InvariantCulture)} 32 Td ({Escape(footer)}) Tj ET\n");

            var bytes = Encoding.ASCII.GetBytes(content.ToString());
            BeginObject(firstPageObject + index * 2);
            Write($"<< /Length {bytes.Length} >>\nstream\n");
            output.Write(bytes);
            Write("\nendstream\nendobj\n");
            BeginObject(firstPageObject + index * 2 + 1);
            Write($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {firstPageObject + index * 2} 0 R >>\nendobj\n");
        }

        var xref = output.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
