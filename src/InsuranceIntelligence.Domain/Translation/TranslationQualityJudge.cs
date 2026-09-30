namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Ports the reference Flutter app's <c>judgeTranslation</c>, <c>cleanTranslationOutput</c>
/// and <c>hasRepetitionLoop</c> (build plan §5) into a pure Domain judge. AfriSLM does not fail
/// loudly, it invents, so every documented failure mode gets its own rejection here rather than a
/// single pass/fail: echoing the instruction, prefixing commentary, returning the input unchanged,
/// looping a phrase, emitting the wrong script, or still reading as English.</summary>
public static class TranslationQualityJudge
{
    private const int LengthBlowupMultiplier = 6;
    private const int LengthBlowupPadding = 20;

    private static readonly string[] EchoPrefixes =
    [
        "here is the translation", "here's the translation", "translation:", "the translation is",
        "sure, here is the translation", "sure, here's the translation",
    ];

    private static readonly HashSet<string> CommonEnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "is", "are", "was", "were", "have", "has", "will", "would", "please",
        "thank", "thanks", "you", "your", "claim", "policy", "insurance", "with", "this", "that",
        "for", "from", "what", "when", "where", "how", "can", "could", "should", "our", "team",
    };

    /// <summary>Strips a wrapping quote and a leading "here is the translation"-style prefix, the
    /// two forms of commentary the model prepends instead of producing only the translation.</summary>
    public static string Clean(string rawOutput)
    {
        if (string.IsNullOrEmpty(rawOutput))
        {
            return string.Empty;
        }

        var text = rawOutput.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1].Trim();
        }

        foreach (var prefix in EchoPrefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stripped = text[prefix.Length..].TrimStart(':', ' ', '\t', '-');
            if (!string.IsNullOrWhiteSpace(stripped))
            {
                text = stripped;
            }

            break;
        }

        return text.Trim();
    }

    /// <summary>A short phrase (two to four words) repeating three times in a row, the model's
    /// documented failure of looping until the token budget runs out.</summary>
    public static bool HasRepetitionLoop(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 6)
        {
            return false;
        }

        for (var phraseLength = 2; phraseLength <= 4; phraseLength++)
        {
            for (var start = 0; start + (phraseLength * 3) <= words.Length; start++)
            {
                var phrase = words.Skip(start).Take(phraseLength).ToArray();
                var second = words.Skip(start + phraseLength).Take(phraseLength).ToArray();
                var third = words.Skip(start + (phraseLength * 2)).Take(phraseLength).ToArray();
                if (phrase.SequenceEqual(second, StringComparer.OrdinalIgnoreCase) &&
                    phrase.SequenceEqual(third, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Deliberately hard to trigger on African-language input: a short reply never
    /// flips this, and it takes several common English function words, not just one loanword,
    /// before the text is judged to still be English.</summary>
    public static bool LooksLikeEnglish(string text)
    {
        var words = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('.', ',', '!', '?', ';', ':', '"', '\''))
            .Where(word => word.Length > 0)
            .ToArray();

        if (words.Length < 4)
        {
            return false;
        }

        var hits = words.Count(word => CommonEnglishWords.Contains(word));
        return hits >= 3 && hits >= words.Length / 3.0;
    }

    /// <summary>Luganda and Kiswahili are both written in the Latin alphabet; a translation that
    /// is mostly letters outside it emitted the wrong script rather than the wrong words.</summary>
    public static bool HasGarbledScript(string text)
    {
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length == 0)
        {
            return false;
        }

        var nonLatin = letters.Count(letter => letter is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z')));
        return nonLatin > letters.Length / 4.0;
    }

    /// <summary>Covers both providers' own tuned prompt wording: AfriSlmTranslationProvider's
    /// ("professional ... translator", "produce only the", "please translate the following") and
    /// SunbirdTranslationProvider's ("verbatim translation engine", "output only the",
    /// "text to translate ... between the markers"), so this check is not silently blind to one
    /// engine echoing its own instruction back.</summary>
    private static bool LooksLikeEchoedPrompt(string text)
    {
        var lowered = text.ToLowerInvariant();
        return (lowered.Contains("professional") && lowered.Contains("translator"))
            || lowered.Contains("produce only the")
            || lowered.Contains("please translate the following")
            || lowered.Contains("verbatim translation engine")
            || lowered.Contains("output only the")
            || lowered.Contains("between the markers");
    }

    private static int CountWords(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>The single entry point: cleans <paramref name="candidateOutput"/> and checks it
    /// against every documented failure mode in order. Returns null when the candidate passes.
    /// <paramref name="candidateShouldNotBeEnglish"/> is false on a retry attempt (build plan §5,
    /// "strict English check on the first attempt only"), and is always false when translating
    /// into English, where the candidate is supposed to read as English.</summary>
    public static TranslationFailureReason? Judge(string sourceText, string candidateOutput, bool candidateShouldNotBeEnglish)
    {
        var cleaned = Clean(candidateOutput);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return TranslationFailureReason.Empty;
        }

        if (string.Equals(cleaned.Trim(), sourceText.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return TranslationFailureReason.Unchanged;
        }

        if (LooksLikeEchoedPrompt(candidateOutput))
        {
            return TranslationFailureReason.EchoedPrompt;
        }

        if (HasRepetitionLoop(cleaned))
        {
            return TranslationFailureReason.RepetitionLoop;
        }

        var sourceWords = CountWords(sourceText);
        var candidateWords = CountWords(cleaned);
        if (sourceWords > 0 && candidateWords > (sourceWords * LengthBlowupMultiplier) + LengthBlowupPadding)
        {
            return TranslationFailureReason.LengthBlowup;
        }

        if (candidateShouldNotBeEnglish && LooksLikeEnglish(cleaned))
        {
            return TranslationFailureReason.StillEnglish;
        }

        if (HasGarbledScript(cleaned))
        {
            return TranslationFailureReason.GarbledScript;
        }

        return null;
    }
}
