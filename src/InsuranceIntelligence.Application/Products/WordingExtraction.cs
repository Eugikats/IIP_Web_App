using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Products;

/// <summary>What the extraction needs to know about the product and the tenant to judge each
/// proposed item: which document types exist (a requirement can only name one of them) and what the
/// product already has.</summary>
public sealed record WordingExtractionContext(
    IReadOnlyList<string> DocumentTypeKeys,
    IReadOnlyList<string> ExistingClaimTypeNames,
    IReadOnlyList<string> ExistingDocumentKeys,
    IReadOnlyList<string> ExistingFieldLabels);

public sealed class WordingExtractionException(string message) : InvalidOperationException(message);

/// <summary>
/// The prompt sent to the model and the strict reading of its answer (build-loop item 7). The model
/// only proposes: every item must quote the clause it relies on, and a quote that is not actually in
/// the wording is kept but flagged and cannot be accepted, so an invented clause never reaches a
/// product. Pure functions, no provider or storage, so the rules are testable without a model.
/// </summary>
public static partial class WordingExtraction
{
    /// <summary>Present in the prompt so a deterministic stand-in model can recognise the task.</summary>
    public const string PromptMarker = "IIP-WORDING-EXTRACTION";
    public const string WordingStart = "---WORDING---";
    public const string WordingEnd = "---END WORDING---";
    public const string DocumentKeysLabel = "DOCUMENT TYPE KEYS:";

    /// <summary>How much of a wording the model is given. A longer wording is read from the start
    /// and the draft says it was cut.</summary>
    public const int MaxCharacters = 60_000;
    public const int MaxItems = 80;
    private const int MinQuoteLetters = 12;

    public static string BuildPrompt(string documentName, string wordingText, IReadOnlyList<string> documentTypeKeys)
    {
        var builder = new StringBuilder();
        builder.AppendLine(PromptMarker);
        builder.AppendLine("You read an insurance policy wording and propose how to set up claims handling for it. You only propose; a person reviews every item.");
        builder.AppendLine("Rules:");
        builder.AppendLine("- Use only what the wording says. Never invent a clause, a document or a question.");
        builder.AppendLine("- Every item must include \"quote\": the exact words from the wording it relies on, copied character for character, at most 300 characters.");
        builder.AppendLine("- Do not decide any claim, price or payment. Do not write legal advice.");
        builder.AppendLine("- Answer with one JSON object and nothing else: {\"items\":[...]}.");
        builder.AppendLine("Item kinds and their fields:");
        builder.AppendLine("- covered: something the wording covers. Fields: kind, title, detail, quote, clause.");
        builder.AppendLine("- exclusion: something the wording does not cover. Same fields.");
        builder.AppendLine("- claim_type: a kind of claim the policy handles. Fields: kind, title, detail, quote, clause, aliases (other words people use).");
        builder.AppendLine("- document: a document the policy requires for a claim. Fields: kind, title (what the customer sees), detail (why it is needed), quote, clause, documentKey (exactly one of the DOCUMENT TYPE KEYS, or empty if none fits), claimTypes (names of the claim types it applies to, empty for all), required (true or false).");
        builder.AppendLine("- field: a piece of information the policy asks for when a claim is made. Fields: kind, title (the question), detail (help text), quote, clause, fieldType (Text, Number, Date, Select, Phone, Currency or YesNo), options (for Select), claimTypes, required.");
        builder.AppendLine($"{DocumentKeysLabel} {string.Join(", ", documentTypeKeys)}");
        builder.AppendLine($"Document name: {documentName}");
        builder.AppendLine(WordingStart);
        builder.AppendLine(wordingText);
        builder.AppendLine(WordingEnd);
        return builder.ToString();
    }

    /// <summary>The text the model is given: the chunks in order, cut to <see cref="MaxCharacters"/>.</summary>
    public static (string Text, bool Truncated) Prepare(string wordingText)
    {
        var text = wordingText ?? string.Empty;
        return text.Length > MaxCharacters ? (text[..MaxCharacters], true) : (text, false);
    }

    public static IReadOnlyList<WordingDraftItem> Parse(string modelOutput, string wordingText, WordingExtractionContext context)
    {
        var json = ExtractJson(modelOutput);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new WordingExtractionException("The model's answer could not be read as a list of items. Try again.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                throw new WordingExtractionException("The model's answer did not contain a list of items. Try again.");
            }

            var squashed = Squash(wordingText);
            var result = new List<WordingDraftItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in items.EnumerateArray())
            {
                if (result.Count >= MaxItems || element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var item = ReadItem(element, result.Count + 1, wordingText, squashed, context);
                if (item is not null && seen.Add($"{item.Kind}|{Normalize(item.Title)}|{string.Join(",", item.ClaimTypeNames.Select(Normalize))}"))
                {
                    result.Add(item);
                }
            }

            return result;
        }
    }

    private static WordingDraftItem? ReadItem(JsonElement element, int number, string wordingText, Squashed squashed, WordingExtractionContext context)
    {
        if (!TryKind(Text(element, "kind"), out var kind))
        {
            return null;
        }

        var title = Clip(Text(element, "title"), 120);
        var quote = Clip(Text(element, "quote"), 600);
        if (title.Length == 0 || quote.Length == 0)
        {
            return null;
        }

        var citation = Cite(quote, Text(element, "clause"), wordingText, squashed);
        string? problem = citation.Verified ? null : "The quoted words were not found in the wording, so this cannot be accepted. Reject it or run the reading again.";
        string? note = null;
        var item = new WordingDraftItem
        {
            ItemId = $"i{number}",
            Kind = kind,
            Title = title,
            Detail = Clip(Text(element, "detail"), 500),
            Citation = citation,
            Required = !element.TryGetProperty("required", out var required) || required.ValueKind != JsonValueKind.False,
        };

        switch (kind)
        {
            case WordingItemKind.ClaimType:
                item = item with { Aliases = List(element, "aliases", 12, 60) };
                if (context.ExistingClaimTypeNames.Any(name => Normalize(name) == Normalize(title)))
                {
                    note = "The product already has this claim type; accepting it changes nothing.";
                }

                break;
            case WordingItemKind.Document:
            {
                var requested = Text(element, "documentKey").Trim();
                var canonical = context.DocumentTypeKeys.FirstOrDefault(key => string.Equals(key, requested, StringComparison.OrdinalIgnoreCase));
                item = item with { DocumentKey = canonical, ClaimTypeNames = List(element, "claimTypes", 20, 60) };
                if (canonical is null)
                {
                    problem ??= requested.Length == 0
                        ? "No document type matches this document. Add one under document types, or reject this item."
                        : $"There is no document type called '{requested}'. Add it under document types, or reject this item.";
                }
                else if (context.ExistingDocumentKeys.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                {
                    note = "The product already lists this document; it is added for the claim types named only if they are not already covered.";
                }

                break;
            }

            case WordingItemKind.Field:
            {
                var type = Enum.TryParse<ClaimFieldType>(Text(element, "fieldType").Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                    ? parsed
                    : ClaimFieldType.Text;
                var options = List(element, "options", 30, 80);
                if (type == ClaimFieldType.Select && options.Count < 2)
                {
                    type = ClaimFieldType.Text;
                    options = [];
                }

                item = item with { FieldType = type.ToString(), Options = options, ClaimTypeNames = List(element, "claimTypes", 20, 60) };
                if (context.ExistingFieldLabels.Any(label => Normalize(label) == Normalize(title)))
                {
                    note = "The product already asks this question; accepting it changes nothing.";
                }

                break;
            }
        }

        return item with { Problem = problem, Note = note };
    }

    // ---- citation checking ----

    /// <summary>The wording reduced to lower-case letters and digits, with a map back to where each
    /// kept character sat in the original, so a quote can be found whatever its spacing and
    /// punctuation and then located for its page and clause.</summary>
    public readonly record struct Squashed(string Text, int[] Map);

    public static Squashed Squash(string text)
    {
        var builder = new StringBuilder(text.Length);
        var map = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetterOrDigit(text[i]))
            {
                builder.Append(char.ToLowerInvariant(text[i]));
                map.Add(i);
            }
        }

        return new Squashed(builder.ToString(), map.ToArray());
    }

    public static WordingCitation Cite(string quote, string? clauseFromModel, string wordingText, Squashed squashed)
    {
        var needle = Squash(quote).Text;
        var index = needle.Length >= MinQuoteLetters ? squashed.Text.IndexOf(needle, StringComparison.Ordinal) : -1;
        if (index < 0)
        {
            return new WordingCitation(quote, NormalizeOrNull(clauseFromModel), null, Verified: false);
        }

        var position = squashed.Map[index];
        return new WordingCitation(quote, NormalizeOrNull(clauseFromModel) ?? ClauseBefore(wordingText, position), PageAt(wordingText, position), Verified: true);
    }

    /// <summary>Form feeds mark pages in extracted text; failing that, a "Page 2" line does.</summary>
    public static int? PageAt(string text, int position)
    {
        var before = text[..Math.Min(position + 1, text.Length)];
        var formFeeds = before.Count(character => character == '\f');
        if (formFeeds > 0 || text.Contains('\f'))
        {
            return formFeeds + 1;
        }

        var marker = PageMarker().Matches(before);
        return marker.Count > 0 && int.TryParse(marker[^1].Groups[1].Value, out var page) ? page : null;
    }

    public static string? ClauseBefore(string text, int position)
    {
        var before = text[..Math.Min(position + 6, text.Length)];
        var matches = ClauseNumber().Matches(before);
        return matches.Count > 0 ? matches[^1].Groups[1].Value : null;
    }

    [GeneratedRegex(@"\bPage\s+(\d{1,4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex PageMarker();

    [GeneratedRegex(@"(?<![\d.])(\d{1,2}(?:\.\d{1,2})+)(?=\s)")]
    private static partial Regex ClauseNumber();

    // ---- reading the model's JSON ----

    private static string ExtractJson(string output)
    {
        var text = output ?? string.Empty;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static bool TryKind(string value, out WordingItemKind kind)
    {
        switch (Normalize(value))
        {
            case "covered":
            case "cover":
            case "coverage":
                kind = WordingItemKind.Covered;
                return true;
            case "exclusion":
            case "excluded":
            case "exclude":
                kind = WordingItemKind.Exclusion;
                return true;
            case "claimtype":
                kind = WordingItemKind.ClaimType;
                return true;
            case "document":
            case "requireddocument":
                kind = WordingItemKind.Document;
                return true;
            case "field":
            case "question":
                kind = WordingItemKind.Field;
                return true;
            default:
                kind = WordingItemKind.Covered;
                return false;
        }
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static IReadOnlyList<string> List(JsonElement element, string name, int max, int maxLength)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.String)
            .Select(entry => Clip(entry.GetString() ?? string.Empty, maxLength))
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToArray();
    }

    private static string Clip(string value, int max)
    {
        var trimmed = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }

    private static string? NormalizeOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : Clip(value, 40);

    internal static string Normalize(string? value) => ClaimTypeMatching.Normalize(value);
}
