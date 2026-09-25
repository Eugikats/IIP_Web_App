using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Non-negotiable 7 (build plan §4): policy numbers, amounts, dates and phone numbers
/// survive a translation round trip unchanged. Each match is swapped for an invisible, index-only
/// placeholder before the text reaches the model, and swapped back afterwards, so the model never
/// sees the real value and has nothing to mistranslate or renumber.</summary>
public static class ProtectedTokenMasker
{
    private static readonly Regex PolicyNumberPattern = new(@"[A-Z]{2,4}/[A-Z]{2}/\d{4}/\d{4,6}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountPattern = new(
        @"(?:UGX|KES|USD|TZS)\s?[\d,]+(?:\.\d+)?|\b[\d,]+(?:\.\d+)?\s?(?:UGX|KES|USD|TZS)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}/\d{1,2}/\d{2,4}\b", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"\+?\d{9,15}\b", RegexOptions.Compiled);

    /// <summary>Word joiner (U+2060) either side of the index: invisible in a rendered reply, and
    /// unlikely to be dropped or reworded by the model the way plain digits or brackets might be.</summary>
    private const char Marker = '⁠';

    public static (string Masked, IReadOnlyDictionary<string, string> Tokens) Mask(string text)
    {
        var tokens = new Dictionary<string, string>();
        var index = 0;

        string ReplaceWith(Match match)
        {
            var placeholder = $"{Marker}TKN{index}{Marker}";
            tokens[placeholder] = match.Value;
            index++;
            return placeholder;
        }

        var masked = PolicyNumberPattern.Replace(text, ReplaceWith);
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
