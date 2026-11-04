namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// Works out which of a claim's slots a customer meant when they sent a file on WhatsApp with a caption such as "police
/// report" (build-loop item 27). It is deliberately strict: every meaningful word of the caption must appear in exactly one
/// slot's name, and that slot must be one that still has room for the file. Anything less sure returns nothing, and the
/// file is left to the document recogniser and, failing that, to a person. A wrong guess would put a paper in the wrong
/// place on a claim; no guess only costs a look.
/// </summary>
public static class SlotCaptionMatcher
{
    private static readonly HashSet<string> Filler = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "this", "that", "here", "from", "with", "of", "my", "your", "our", "is", "are", "it", "a", "an",
        "attached", "attachment", "sending", "send", "sent", "see", "please", "pls", "plz", "photo", "picture", "pic", "image", "file", "scan", "copy", "claim",
    };

    public static ResolvedDocumentSlot? Match(string? caption, IReadOnlyList<ResolvedDocumentSlot> slots, IReadOnlyCollection<CaseDocument> existingDocuments)
    {
        var wanted = Words(caption).Where(word => !Filler.Contains(word)).Distinct(StringComparer.Ordinal).ToArray();
        if (wanted.Length == 0)
        {
            return null;
        }

        var candidates = slots
            .Where(slot => !slot.AllowsMultipleFiles)
            .Where(slot => !existingDocuments.Any(document => !document.IsSuperseded && string.Equals(document.EffectiveKey, slot.Key, StringComparison.OrdinalIgnoreCase)))
            .Where(slot =>
            {
                var named = Words($"{slot.Label} {slot.Key} {slot.Description}").ToHashSet(StringComparer.Ordinal);
                return wanted.All(named.Contains);
            })
            .Take(2)
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static IEnumerable<string> Words(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // A camel-case key such as "PoliceAbstract" becomes two words; trailing "s" is dropped so "reports" meets "report".
        var spaced = new System.Text.StringBuilder();
        foreach (var character in text)
        {
            if (char.IsUpper(character) && spaced.Length > 0 && char.IsLower(spaced[^1]))
            {
                spaced.Append(' ');
            }

            spaced.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return spaced.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 3)
            .Select(word => word.Length > 3 && word.EndsWith('s') ? word[..^1] : word);
    }
}
