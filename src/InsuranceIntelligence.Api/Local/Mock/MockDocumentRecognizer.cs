using System.Text;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// Deterministic stand-in for a vision model that recognises documents, for local runs and tests.
/// It never looks at a picture. A file containing <c>IIP-MOCK-DOC:TypeKey</c> (any key in the
/// workspace catalogue, built in or the insurer's own) is recognised as that type with high
/// confidence; <c>IIP-MOCK-CONFIDENCE:0.5</c> in the same file sets a different confidence. A plain
/// text file that names a catalogue label ("Police report") is recognised from that. Anything else
/// is "could not tell". The same fixture always gives the same answer.
/// </summary>
public sealed partial class MockDocumentRecognizer : IDocumentRecognizer
{
    public const string ProviderName = "Mock";
    public const string ConfidenceMarker = "IIP-MOCK-CONFIDENCE:";
    private const int ScanBytes = 4 * 1024 * 1024;

    public async Task<DocumentRecognition> RecognizeAsync(DocumentRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        string scanned;
        await using (var stream = await request.OpenAsync(cancellationToken))
        {
            var buffer = new byte[ScanBytes];
            var read = 0;
            int chunk;
            while (read < buffer.Length && (chunk = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken)) > 0)
            {
                read += chunk;
            }

            scanned = Encoding.Latin1.GetString(buffer, 0, read);
        }

        var now = DateTimeOffset.UtcNow;
        var confidence = ParseConfidence(scanned);

        var marker = MarkerPattern().Match(scanned);
        if (marker.Success)
        {
            var entry = request.Catalogue.FirstOrDefault(candidate => string.Equals(candidate.Key, marker.Groups["key"].Value, StringComparison.OrdinalIgnoreCase));
            return entry is null
                ? new DocumentRecognition(null, 0.2m, "It looks like a document type this workspace does not have.", ProviderName, "mock-v1", now)
                : new DocumentRecognition(entry.Key, confidence ?? 0.95m, $"The text on it says it is a {entry.Label.ToLowerInvariant()}.", ProviderName, "mock-v1", now);
        }

        var byLabel = request.Catalogue
            .Where(entry => scanned.Contains(entry.Label, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (byLabel.Length == 1)
        {
            return new DocumentRecognition(byLabel[0].Key, confidence ?? 0.9m, $"The words \"{byLabel[0].Label}\" appear on it.", ProviderName, "mock-v1", now);
        }

        return new DocumentRecognition(null, confidence ?? 0.2m, "Nothing on it matched a known type of document.", ProviderName, "mock-v1", now);
    }

    private static decimal? ParseConfidence(string scanned)
    {
        var at = scanned.IndexOf(ConfidenceMarker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var tail = scanned[(at + ConfidenceMarker.Length)..];
        var match = NumberPattern().Match(tail);
        return match.Success && decimal.TryParse(match.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0m, 1m)
            : null;
    }

    [GeneratedRegex(@"IIP-MOCK-DOC:(?<key>[A-Za-z0-9_\-]+)")]
    private static partial Regex MarkerPattern();

    [GeneratedRegex(@"^\s*(\d+(\.\d+)?)")]
    private static partial Regex NumberPattern();
}
