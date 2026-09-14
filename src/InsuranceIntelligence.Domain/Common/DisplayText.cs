namespace InsuranceIntelligence.Domain.Common;

/// <summary>
/// Normalises model-generated prose before a person reads it.
///
/// Language models lean heavily on the em dash, and readers now treat it as the plainest sign that
/// text was machine-written. That is the opposite of what an insurer's staff and customers should
/// feel reading a claim review or a customer update, so every model completion passes through here
/// once, at the provider boundary, rather than each screen trying to catch it.
///
/// A spaced em dash (the way models almost always use it) becomes a comma, which reads correctly in
/// nearly every sentence it appears in. An unspaced em dash becomes a plain hyphen. Spaced en
/// dashes get the same treatment, because models substitute one for the other; unspaced en dashes
/// are left alone, since those are number ranges.
///
/// The dash characters are written as numeric code points, never as literals. A literal em dash in
/// this file is exactly what a repository-wide clean-up would rewrite, silently turning this helper
/// into one that mangles ordinary hyphens and lets real em dashes through.
/// </summary>
public static class DisplayText
{
    private static readonly char EmDash = (char)0x2014;
    private static readonly char EnDash = (char)0x2013;

    public static string WithoutEmDashes(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var spacedEm = $" {EmDash} ";
        var spacedEn = $" {EnDash} ";
        if (text.IndexOf(EmDash) < 0 && !text.Contains(spacedEn, StringComparison.Ordinal))
        {
            return text;
        }

        return text
            .Replace(spacedEm, ", ", StringComparison.Ordinal)
            .Replace(spacedEn, ", ", StringComparison.Ordinal)
            .Replace($"{EmDash} ", ", ", StringComparison.Ordinal)
            .Replace($" {EmDash}", ",", StringComparison.Ordinal)
            .Replace(EmDash.ToString(), "-", StringComparison.Ordinal);
    }
}
