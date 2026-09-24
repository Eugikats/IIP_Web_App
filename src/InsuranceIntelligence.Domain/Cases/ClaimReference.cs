using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// The short reference people use for a claim: "CLM-" plus the first eight hex characters of its
/// id, upper case (for example CLM-726E3445). Staff screens, the assistant and model prompts use
/// this; the full id stays internal, because a 36-character GUID on screen reads as a system error
/// to anyone outside engineering.
///
/// Derived, never stored, so every existing claim already has one and nothing needs migrating.
/// Eight hex characters give roughly four billion values, which is ample inside one workspace. If
/// two claims ever did share a prefix, lookup by reference returns both and the caller asks which
/// one was meant rather than guessing.
/// </summary>
public static partial class ClaimReference
{
    private const string Prefix = "CLM-";

    public static string For(Guid caseId) => Prefix + caseId.ToString("N")[..8].ToUpperInvariant();

    public static bool Matches(Guid caseId, string? reference) =>
        !string.IsNullOrWhiteSpace(reference)
        && string.Equals(For(caseId), reference.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Every CLM-XXXXXXXX token in free text, normalised to upper case, in order of
    /// appearance and without duplicates.</summary>
    public static IReadOnlyList<string> FindIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return ReferencePattern().Matches(text)
            .Select(match => Prefix + match.Groups["hex"].Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    [GeneratedRegex(@"\bCLM-(?<hex>[0-9A-Fa-f]{8})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
}
