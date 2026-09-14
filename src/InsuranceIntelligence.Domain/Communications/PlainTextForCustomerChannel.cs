using System.Text;
using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Communications;

/// <summary>
/// Converts the markdown a model emits into clean plain text for channels that render none of it.
///
/// Staff surfaces render assistant markdown properly (web/portal's MarkdownText). SMS and WhatsApp
/// do not: SMS has no formatting at all, and WhatsApp's own syntax is *single* asterisks, so the
/// **double** asterisks a model emits are not bold there — a policyholder simply receives the
/// literal characters. Anything model-generated that crosses into one of those channels has to be
/// flattened first, or the customer reads raw markup.
///
/// Deliberately a strip-and-reflow, not a translation into WhatsApp's own syntax: the same text
/// may go out over SMS (no formatting), WhatsApp (its own syntax), or a future channel, and
/// emphasis is never load-bearing in these messages — the words carry the meaning. Flattening once
/// is predictable everywhere; per-channel markup would need a different transform per channel and
/// would still be wrong the moment a message is retried over a different one.
/// </summary>
public static class PlainTextForCustomerChannel
{
    // **bold**, *italic*, _italic_, `code` — capture the inner text, drop the delimiters.
    private static readonly Regex Bold = new(@"\*\*(?<text>[^*]+)\*\*", RegexOptions.Compiled);
    private static readonly Regex Italic = new(@"(?<![*\w])\*(?<text>[^*\n]+)\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex Underscore = new(@"(?<![_\w])_(?<text>[^_\n]+)_(?!_)", RegexOptions.Compiled);
    private static readonly Regex Code = new(@"`(?<text>[^`]+)`", RegexOptions.Compiled);
    private static readonly Regex Heading = new(@"^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^\s*[-*+]\s+", RegexOptions.Compiled);
    private static readonly Regex Numbered = new(@"^(\s*)(\d+)[.)]\s+", RegexOptions.Compiled);
    private static readonly Regex ExcessBlankLines = new(@"(\r?\n){3,}", RegexOptions.Compiled);

    /// <summary>Bullet marker used in place of markdown's "- ". A real bullet character rather than
    /// a hyphen, so a flattened list still reads as a list on a phone.</summary>
    private const string BulletMarker = "• ";

    public static string Convert(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalized = markdown.Replace("\r\n", "\n");
        var builder = new StringBuilder();

        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = Heading.Replace(rawLine, string.Empty);

            // Lists: keep the item on its own line, swap the markdown marker for a readable one.
            // Numbered lists keep their number, which carries ordering the words rely on.
            var numbered = Numbered.Match(line);
            if (numbered.Success)
            {
                line = Numbered.Replace(line, $"{numbered.Groups[2].Value}. ");
            }
            else if (Bullet.IsMatch(line))
            {
                line = Bullet.Replace(line, BulletMarker);
            }

            // Inline emphasis: strip the delimiters, keep the words. Bold first, so the ** of a
            // bold span is consumed before the single-* italic pattern can see either half of it.
            line = Bold.Replace(line, "${text}");
            line = Code.Replace(line, "${text}");
            line = Italic.Replace(line, "${text}");
            line = Underscore.Replace(line, "${text}");

            builder.Append(line.TrimEnd()).Append('\n');
        }

        // A model's blank-line-separated paragraphs are worth keeping (they are the only structure
        // left once markers are gone), but never more than one blank line in a row on a phone.
        return ExcessBlankLines.Replace(builder.ToString(), "\n\n").Trim();
    }
}
