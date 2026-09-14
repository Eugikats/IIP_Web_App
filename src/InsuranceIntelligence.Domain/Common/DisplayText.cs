namespace InsuranceIntelligence.Domain.Common;

/// <summary>
/// Normalises model-generated prose before a person reads it.
///
/// Language models lean heavily on the em dash, and readers now treat it as the plainest sign that
/// text was machine-written. That is the opposite of what an insurer's staff and customers should
/// feel reading a claim review or a customer update, so every model completion passes through here
/// once, at the provider boundary, rather than each screen trying to catch it.
///
/// A spaced dash (the way models almost always use it) becomes a comma, which reads correctly in
/// nearly every sentence it appears in. An unspaced em dash becomes a plain hyphen. Spaced en
/// dashes get the same treatment as em dashes, because models substitute them for one another;
/// unspaced en dashes are left alone, since those are number ranges ("2020–2021").
/// </summary>
public static class DisplayText
{
    private const char EmDash = '—';
    private const char EnDash = '–';

    public static string WithoutEmDashes(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        if (text.IndexOf(EmDash) < 0 && text.IndexOf($" {EnDash} ", StringComparison.Ordinal) < 0)
        {
            return text;
        }

        return text
            .Replace($" {EmDash} ", ", ", StringComparison.Ordinal)
            .Replace($" {EnDash} ", ", ", StringComparison.Ordinal)
            .Replace($"{EmDash} ", ", ", StringComparison.Ordinal)
            .Replace($" {EmDash}", ",", StringComparison.Ordinal)
            .Replace(EmDash.ToString(), "-", StringComparison.Ordinal);
    }
}
