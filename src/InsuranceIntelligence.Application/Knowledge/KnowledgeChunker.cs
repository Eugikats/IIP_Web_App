using System.Text;
using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Application.Knowledge;

public sealed record TextChunk(string Content, string? Section);

/// <summary>
/// Cuts a policy document into pieces for retrieval. A piece is whole paragraphs, never sliced through a word or a
/// sentence; it remembers the heading it sits under so an answer can cite "Exclusions" rather than "chunk 7"; and
/// it repeats the last sentence of the piece before it, so a rule that runs across a boundary is still found whole.
/// The old cut was every 600 characters regardless of meaning, which separated an exclusion from the sentence that
/// introduced it.
/// </summary>
public static class KnowledgeChunker
{
    public const int TargetLength = 800;
    public const int MaxLength = 1400;
    private const int OverlapLength = 220;

    private static readonly Regex Numbered = new(@"^\s*(\d+(\.\d+)*|[A-Z]|[IVX]+)[.)]\s+\S", RegexOptions.Compiled);
    private static readonly Regex SentenceEnd = new(@"(?<=[.!?;])\s+(?=[A-Z0-9(""'])", RegexOptions.Compiled);

    public static IReadOnlyList<TextChunk> Chunk(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var chunks = new List<TextChunk>();
        var section = (string?)null;
        var current = new StringBuilder();
        string? currentSection = null;
        var overlap = string.Empty;

        void Flush()
        {
            var content = current.ToString().Trim();
            if (content.Length == 0)
            {
                current.Clear();
                return;
            }

            chunks.Add(new TextChunk(content, currentSection));
            overlap = LastSentence(content);
            current.Clear();
        }

        foreach (var block in Paragraphs(text))
        {
            if (IsHeading(block))
            {
                // A new heading starts a new piece, so a piece never mixes two sections.
                Flush();
                section = Clean(block).TrimEnd(':');
                overlap = string.Empty;
                continue;
            }

            foreach (var part in Split(Clean(block)))
            {
                if (current.Length > 0 && current.Length + part.Length + 1 > TargetLength)
                {
                    Flush();
                }

                if (current.Length == 0)
                {
                    currentSection = section;
                    if (overlap.Length > 0 && !part.StartsWith(overlap, StringComparison.Ordinal))
                    {
                        current.Append(overlap).Append(' ');
                    }
                }
                else
                {
                    current.Append(' ');
                }

                current.Append(part);
            }
        }

        Flush();
        return chunks;
    }

    /// <summary>Blank-line separated blocks; a run of lines with no blank line between is one block unless a line is a heading.</summary>
    private static IEnumerable<string> Paragraphs(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var block = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                if (block.Length > 0)
                {
                    yield return block.ToString();
                    block.Clear();
                }

                continue;
            }

            if (IsHeading(line))
            {
                if (block.Length > 0)
                {
                    yield return block.ToString();
                    block.Clear();
                }

                yield return line;
                continue;
            }

            if (block.Length > 0)
            {
                block.Append(' ');
            }

            block.Append(line);
        }

        if (block.Length > 0)
        {
            yield return block.ToString();
        }
    }

    /// <summary>A short line with no closing full stop that is numbered, in capitals, or ends in a colon.</summary>
    public static bool IsHeading(string line)
    {
        var text = line.Trim();
        if (text.Length is 0 or > 90 || text.EndsWith('.') || text.Contains("  ") && text.Length > 60)
        {
            return false;
        }

        var letters = text.Count(char.IsLetter);
        if (letters < 3)
        {
            return false;
        }

        var allCaps = text.Where(char.IsLetter).All(char.IsUpper);
        return allCaps || text.EndsWith(':') || (Numbered.IsMatch(text) && text.Split(' ').Length <= 10);
    }

    private static IEnumerable<string> Split(string paragraph)
    {
        if (paragraph.Length <= MaxLength)
        {
            yield return paragraph;
            yield break;
        }

        // A very long paragraph is cut at sentence ends, and only as a last resort at a space.
        var piece = new StringBuilder();
        foreach (var sentence in SentenceEnd.Split(paragraph))
        {
            foreach (var bit in SplitAtSpaces(sentence))
            {
                if (piece.Length > 0 && piece.Length + bit.Length + 1 > TargetLength)
                {
                    yield return piece.ToString();
                    piece.Clear();
                }

                if (piece.Length > 0)
                {
                    piece.Append(' ');
                }

                piece.Append(bit);
            }
        }

        if (piece.Length > 0)
        {
            yield return piece.ToString();
        }
    }

    private static IEnumerable<string> SplitAtSpaces(string sentence)
    {
        var rest = sentence;
        while (rest.Length > MaxLength)
        {
            var cut = rest.LastIndexOf(' ', MaxLength);
            if (cut < MaxLength / 2)
            {
                cut = MaxLength;
            }

            yield return rest[..cut].Trim();
            rest = rest[cut..].Trim();
        }

        if (rest.Length > 0)
        {
            yield return rest;
        }
    }

    private static string LastSentence(string content)
    {
        var sentences = SentenceEnd.Split(content);
        var last = sentences[^1].Trim();
        return last.Length is > 0 and <= OverlapLength ? last : string.Empty;
    }

    private static string Clean(string value) => Regex.Replace(value.Trim(), @"\s+", " ");
}
