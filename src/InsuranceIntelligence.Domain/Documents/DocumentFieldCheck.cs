using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>One detail a kind of document must carry, and the names it may be printed under.</summary>
public sealed record RequiredField(string Key, string Label, IReadOnlyList<string> Names);

public sealed record DocumentFieldCheckResult(
    Guid DocumentId,
    DocumentType DocumentType,
    string DocumentLabel,
    // False when nothing could be read from the document, so its details are unknown rather than missing.
    bool Readable,
    IReadOnlyList<RequiredField> Found,
    IReadOnlyList<RequiredField> Missing);

/// <summary>
/// Which details each kind of document must carry, and whether an uploaded document carries them. Only values the
/// reading service actually extracted (or a person corrected) count as found. A document nothing could be read
/// from is reported as not readable, never as missing every detail, because the right next step differs: a person
/// must read it, not the customer send it again. Matching is on whole words of the field name, so "ob" matches
/// "OB No." and never the middle of another word. Pure and deterministic; it never infers a value.
/// </summary>
public static class DocumentFieldCheck
{
    private static readonly string[] ReadKeyPrefixes = ["field.", "keyvalue."];

    public static IReadOnlyList<RequiredField> RequiredFor(DocumentType type, string productLine)
    {
        var motor = string.Equals(productLine, "Motor", StringComparison.OrdinalIgnoreCase);
        return type switch
        {
            DocumentType.ClaimForm => ClaimFormFields(motor),
            DocumentType.PoliceAbstract => new[]
            {
                new RequiredField("reference", "Police reference (OB number)", ["ob", "ob no", "ob number", "occurrence book", "abstract no", "report no", "reference"]),
                new RequiredField("date", "Date", ["date"]),
                new RequiredField("station", "Police station", ["station", "police station"]),
            },
            DocumentType.Invoice => new[]
            {
                new RequiredField("total", "Total amount", ["total", "amount due", "grand total", "amount"]),
                new RequiredField("vendor", "Who issued it", ["vendor", "supplier", "seller", "company", "issued by"]),
                new RequiredField("date", "Date", ["date", "invoice date"]),
            },
            DocumentType.GarageEstimate => new[]
            {
                new RequiredField("total", "Estimated cost", ["total", "estimate", "amount", "grand total"]),
                new RequiredField("garage", "Garage", ["garage", "vendor", "supplier", "company"]),
            },
            DocumentType.MedicalReport => new[]
            {
                new RequiredField("patient", "Patient name", ["patient", "patient name", "name"]),
                new RequiredField("diagnosis", "Diagnosis", ["diagnosis", "condition", "findings"]),
                new RequiredField("date", "Date", ["date"]),
            },
            DocumentType.CustomerId => new[]
            {
                new RequiredField("id_number", "ID number", ["national id", "nin", "id number", "identification number", "id no", "licence no", "license no", "permit no", "card no"]),
                new RequiredField("name", "Name", ["name", "surname", "given name"]),
            },
            _ => [],
        };
    }

    private static RequiredField[] ClaimFormFields(bool motor)
    {
        var fields = new List<RequiredField>
        {
            new("policy_number", "Policy number", ["policy no", "policy number", "policy"]),
            new("claimant", "Name of the person claiming", ["insured", "claimant", "policyholder", "name of insured", "name of policyholder", "proposer", "full name"]),
            new("loss_date", "Date of the accident or loss", ["date of accident", "date of loss", "date of incident", "accident date", "incident date", "date of occurrence"]),
            new("description", "What happened", ["description", "circumstances", "details of accident", "how did the accident", "brief description"]),
        };
        if (motor)
        {
            fields.Add(new RequiredField("vehicle", "Vehicle registration", ["registration", "reg no", "reg", "vehicle no", "vehicle registration", "number plate", "plate"]));
        }

        return fields.ToArray();
    }

    public static DocumentFieldCheckResult Check(CaseDocument document, DocumentType effectiveType, string productLine, string label)
    {
        var required = RequiredFor(effectiveType, productLine);
        // A person's correction counts as a value, including one for a detail the reading service never found.
        var values = document.Extraction.Fields.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var (key, correction) in document.Extraction.FieldCorrections ?? new Dictionary<string, DocumentFieldCorrection>())
        {
            values[key] = correction.CorrectedValue;
        }

        var fields = values
            .Where(pair => IsReadField(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => Words(pair.Key))
            .ToList();

        if (required.Count == 0)
        {
            return new DocumentFieldCheckResult(document.DocumentId, effectiveType, label, Readable: true, [], []);
        }

        if (fields.Count == 0)
        {
            return new DocumentFieldCheckResult(document.DocumentId, effectiveType, label, Readable: false, [], []);
        }

        var found = required.Where(requirement => fields.Any(words => requirement.Names.Any(name => ContainsRun(words, Words(name))))).ToArray();
        return new DocumentFieldCheckResult(document.DocumentId, effectiveType, label, Readable: true, found, required.Except(found).ToArray());
    }

    private static bool IsReadField(string key) =>
        ReadKeyPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string[] Words(string text)
    {
        var withoutPrefix = ReadKeyPrefixes.Aggregate(text, (current, prefix) =>
            current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? current[prefix.Length..] : current);
        // Camel case and punctuation both separate words: "claimantName", "Policy_No." and "Policy No" read alike.
        var spaced = Regex.Replace(withoutPrefix, "(?<=[a-z])(?=[A-Z])", " ");
        return Regex.Split(spaced.ToLowerInvariant(), "[^a-z0-9]+").Where(word => word.Length > 0).ToArray();
    }

    private static bool ContainsRun(string[] words, string[] run)
    {
        if (run.Length == 0 || run.Length > words.Length) return false;
        for (var start = 0; start <= words.Length - run.Length; start++)
        {
            if (run.Select((word, offset) => words[start + offset] == word).All(match => match)) return true;
        }

        return false;
    }
}
