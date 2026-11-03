using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Products;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// The Mock stand-in for the model when it is asked to read a policy wording. It never calls a
/// model: it reads the numbered clauses of the wording it is given with fixed rules (a section about
/// what is covered, one about what is not, a claims section that says "For an X claim we need: ...",
/// and a list of information asked for) and answers in the same JSON a real model must produce, so
/// the real parser, citation check and review screen are exercised end to end. Every quote is copied
/// from the wording. Wordings laid out differently simply yield fewer items.
/// </summary>
public static partial class MockWordingExtractor
{
    public static bool Handles(string prompt) => prompt.Contains(WordingExtraction.PromptMarker, StringComparison.Ordinal);

    public static string Respond(string prompt)
    {
        var text = WordingBetween(prompt);
        var items = new List<Dictionary<string, object>>();
        var section = string.Empty;
        var claimTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var boundaries = Boundary().Matches(text).ToArray();
        for (var i = 0; i < boundaries.Length; i++)
        {
            var match = boundaries[i];
            var end = i + 1 < boundaries.Length ? boundaries[i + 1].Index : text.Length;
            if (match.Groups["section"].Success)
            {
                section = match.Groups["section"].Value.Trim().ToUpperInvariant();
                continue;
            }

            if (match.Groups["info"].Success)
            {
                AddInformationFields(items, text[(match.Index + match.Length)..end]);
                continue;
            }

            if (!match.Groups["clause"].Success)
            {
                continue;
            }

            var clause = match.Groups["clause"].Value;
            var body = text[(match.Index + match.Length)..end].Trim();
            if (body.Length < 12)
            {
                continue;
            }

            if (section.Contains("NOT COVERED", StringComparison.Ordinal) || section.Contains("EXCLUSION", StringComparison.Ordinal))
            {
                items.Add(Item("exclusion", TitleOf(body), body, Quote(body), clause));
            }
            else if (section.Contains("COVER", StringComparison.Ordinal))
            {
                items.Add(Item("covered", TitleOf(body), body, Quote(body), clause));
            }
            else if (section.Contains("CLAIM", StringComparison.Ordinal))
            {
                AddClaimRequirements(items, claimTypes, body, clause);
            }
        }

        return JsonSerializer.Serialize(new { items = items.Take(60).ToArray() });
    }

    private static string WordingBetween(string prompt)
    {
        var start = prompt.IndexOf(WordingExtraction.WordingStart, StringComparison.Ordinal);
        var end = prompt.LastIndexOf(WordingExtraction.WordingEnd, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return string.Empty;
        }

        return string.Join(' ', prompt[(start + WordingExtraction.WordingStart.Length)..end].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void AddClaimRequirements(List<Dictionary<string, object>> items, HashSet<string> claimTypes, string body, string clause)
    {
        var match = ClaimNeeds().Match(body);
        if (!match.Success)
        {
            return;
        }

        var claimType = Capitalise(match.Groups["type"].Value.Trim());
        if (claimTypes.Add(claimType))
        {
            items.Add(Item("claim_type", claimType, $"A kind of claim named in clause {clause} of the wording.", match.Value.Trim(), clause));
        }

        foreach (var raw in SplitList(match.Groups["list"].Value))
        {
            var label = Label(raw);
            if (label.Length < 3)
            {
                continue;
            }

            var item = Item("document", label, $"The wording asks for it for a {claimType.ToLowerInvariant()} claim.", raw, clause);
            item["documentKey"] = DocumentKeyFor(raw);
            item["claimTypes"] = new[] { claimType };
            item["required"] = true;
            items.Add(item);
        }
    }

    private static void AddInformationFields(List<Dictionary<string, object>> items, string list)
    {
        foreach (var raw in list.Trim().TrimEnd('.').Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length < 12)
            {
                continue;
            }

            var item = Item("field", Capitalise(raw), "Asked for when a claim is made.", raw, null);
            item["fieldType"] = raw.StartsWith("whether", StringComparison.OrdinalIgnoreCase)
                ? "YesNo"
                : raw.Contains("date", StringComparison.OrdinalIgnoreCase) ? "Date" : "Text";
            item["required"] = true;
            items.Add(item);
        }
    }

    private static Dictionary<string, object> Item(string kind, string title, string detail, string quote, string? clause)
    {
        var item = new Dictionary<string, object>
        {
            ["kind"] = kind,
            ["title"] = title,
            ["detail"] = detail,
            ["quote"] = quote,
        };
        if (clause is not null)
        {
            item["clause"] = clause;
        }

        return item;
    }

    private static string TitleOf(string body)
    {
        var sentence = body.IndexOf(". ", StringComparison.Ordinal);
        if (sentence is > 2 and <= 60)
        {
            return body[..sentence].Trim();
        }

        var words = body.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(7);
        return Capitalise(string.Join(' ', words).TrimEnd('.', ',', ';'));
    }

    private static string Quote(string body) => body.Length <= 250 ? body : body[..250].TrimEnd();

    private static IEnumerable<string> SplitList(string list) =>
        ListSeparator().Split(list.Trim().TrimEnd('.')).Select(part => part.Trim()).Where(part => part.Length > 0);

    private static string Label(string raw)
    {
        var withoutBrackets = Brackets().Replace(raw, " ");
        var withoutLead = Lead().Replace(withoutBrackets.Trim(), string.Empty);
        var firstClause = withoutLead.Split(" for ", 2, StringSplitOptions.None)[0].Trim();
        return Capitalise(string.Join(' ', firstClause.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    private static string DocumentKeyFor(string raw)
    {
        var text = raw.ToLowerInvariant();
        if (text.Contains("police", StringComparison.Ordinal)) return "PoliceAbstract";
        if (text.Contains("claim form", StringComparison.Ordinal)) return "ClaimForm";
        if (text.Contains("licence", StringComparison.Ordinal) || text.Contains("license", StringComparison.Ordinal) || text.Contains("identity", StringComparison.Ordinal)) return "CustomerId";
        if (text.Contains("photo", StringComparison.Ordinal)) return "PhotoEvidence";
        if (text.Contains("quotation", StringComparison.Ordinal) || text.Contains("estimate", StringComparison.Ordinal)) return "GarageEstimate";
        if (text.Contains("invoice", StringComparison.Ordinal) || text.Contains("receipt", StringComparison.Ordinal)) return "Invoice";
        if (text.Contains("medical", StringComparison.Ordinal)) return "MedicalReport";
        return string.Empty;
    }

    private static string Capitalise(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    [GeneratedRegex(@"(?<section>SECTION\s+\d+\.\s+[A-Z][A-Z ]+?)(?=\s+\d+\.\d+\s)|(?<info>CLAIM INFORMATION WE ASK FOR)|\bPage\s+\d+\b|(?<![\d.])(?<clause>\d{1,2}\.\d{1,2})\s+(?=[A-Z])")]
    private static partial Regex Boundary();

    [GeneratedRegex(@"For (?:an?|the)\s+(?<type>[A-Za-z ]+?)\s+claim(?:s)?\s+we need:\s*(?<list>.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ClaimNeeds();

    [GeneratedRegex(@",\s+and\s+|,\s+|\s+and\s+(?=a |an |the |both |clear )", RegexOptions.IgnoreCase)]
    private static partial Regex ListSeparator();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"^(?:the |an? |both |clear |original |completed |copy of |a copy of |the original )+", RegexOptions.IgnoreCase)]
    private static partial Regex Lead();
}
