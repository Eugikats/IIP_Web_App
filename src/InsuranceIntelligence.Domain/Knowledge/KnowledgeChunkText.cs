using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Knowledge;

/// <summary>
/// A knowledge chunk's stored Content sometimes carries its own trailing citation line (e.g. a
/// footer left in place when the source document was ingested), separate from the structured
/// <see cref="KnowledgeChunk.SourceReference"/> and <see cref="RetrievalResult.Citations"/> that
/// already carry that same information for staff-facing display. Folding raw chunk content
/// straight into a customer-facing answer (as both the Mock and Azure OpenAI assistant providers
/// do) then leaks that line into what the customer reads, and it survives machine translation
/// (seen as "Ensibuko: ..." in a Luganda WhatsApp reply). This strips it defensively before any
/// chunk content is used to build customer-facing prose; nothing is lost since the structured
/// citation fields are untouched.
/// </summary>
public static partial class KnowledgeChunkText
{
    public static string StripTrailingSourceLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        return TrailingSourceLine().Replace(content, string.Empty).TrimEnd();
    }

    // Anchored on \z (the true end of the string, not RegexOptions.Multiline's per-line $),
    // because this runs on one chunk's own Content before multiple chunks are joined together
    // (see call sites): a chunk's trailing citation is always at the end of its own text, however
    // it is later concatenated with the next chunk's.
    [GeneratedRegex(@"\s*Source:[^\r\n]*\.?\s*\z", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingSourceLine();
}
