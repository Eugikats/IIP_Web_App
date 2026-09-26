using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Non-negotiable 7 (build plan §4): policy numbers, amounts, dates and phone numbers
/// survive a translation round trip unchanged. Each match is swapped for an invisible, index-only
/// placeholder before the text reaches the model, and swapped back afterwards, so the model never
/// sees the real value and has nothing to mistranslate or renumber.</summary>
public static class ProtectedTokenMasker
{
    // A secure link must reach the customer byte for byte, and a claim reference is what they quote
    // to a person later. Both appear in almost every reply the assistant sends, so both are masked
    // before the model sees the text. The link is masked first because its characters would
    // otherwise be picked up by the phone-number pattern below.
    private static readonly Regex UrlPattern = new(@"https?://[^\s]*[^\s.,;:!?)]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClaimReferencePattern = new(@"\bCLM-[0-9A-F]{8}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PolicyNumberPattern = new(@"[A-Z]{2,4}/[A-Z]{2}/\d{4}/\d{4,6}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountPattern = new(
        @"(?:UGX|KES|USD|TZS)\s?[\d,]+(?:\.\d+)?|\b[\d,]+(?:\.\d+)?\s?(?:UGX|KES|USD|TZS)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}/\d{1,2}/\d{2,4}\b", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"\+?\d{9,15}\b", RegexOptions.Compiled);

    /// <summary>Word joiner (U+2060) either side of the index: invisible in a rendered reply, and
    /// unlikely to be dropped or reworded by the model the way plain digits or brackets might be.</summary>

    // The placeholder is plain ASCII on purpose. An earlier version wrapped it in invisible word
    // joiners, and the model stripped them, so the placeholder could not be found again and the
    // customer's policy number disappeared. Measured against the real model, every plain ASCII
    // style survived a round trip in both directions and the invisible one lost the token in four
    // of six cases. See TranslationService, which also refuses a translation that dropped one.
    /// <param name="protectedLiterals">Exact strings that must survive unchanged even though no
    /// pattern recognises them, such as the customer's first name.</param>
    public static (string Masked, IReadOnlyDictionary<string, string> Tokens) Mask(
        string text, IEnumerable<string>? protectedLiterals = null)
    {
        var tokens = new Dictionary<string, string>();
        var index = 0;

        string ReplaceWith(Match match)
        {
            var placeholder = $"[TKN{index}]";
            tokens[placeholder] = match.Value;
            index++;
            return placeholder;
        }

        var masked = UrlPattern.Replace(text, ReplaceWith);
        masked = ClaimReferencePattern.Replace(masked, ReplaceWith);
        foreach (var literal in (protectedLiterals ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.Ordinal))
        {
            masked = new Regex($@"\b{Regex.Escape(literal)}\b", RegexOptions.CultureInvariant).Replace(masked, ReplaceWith);
        }

        masked = PolicyNumberPattern.Replace(masked, ReplaceWith);
        masked = AmountPattern.Replace(masked, ReplaceWith);
        masked = DatePattern.Replace(masked, ReplaceWith);
        masked = PhonePattern.Replace(masked, ReplaceWith);

        return (masked, tokens);
    }

    public static string Unmask(string text, IReadOnlyDictionary<string, string> tokens)
    {
        var result = text;
        foreach (var (placeholder, original) in tokens)
        {
            result = result.Replace(placeholder, original);
        }

        return result;
    }
}
