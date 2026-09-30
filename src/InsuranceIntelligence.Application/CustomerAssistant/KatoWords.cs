namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Short answers a customer gives to Kato's questions ("is this right?", "is this a new
/// accident?"), recognised in English, Luganda and Kiswahili. Checked against both what the
/// customer wrote and its English translation, since a one-word reply often translates badly.
/// A binding step (opening a claim) happens only on a clear yes; anything unclear is treated as
/// more detail and read back again.
/// </summary>
public static class KatoWords
{
    private static readonly HashSet<string> Yes = new(StringComparer.Ordinal)
    {
        "yes", "y", "yeah", "yea", "yep", "yup", "ok", "okay", "correct", "right", "sure", "confirm",
        "confirmed", "true", "1", "send", "send it", "yes please", "thats right", "that is right",
        "its right", "it is right", "yes correct", "yes send", "yes send it", "exactly",
        "yee", "ye", "yego", "weewaawo", "kale", "kituufu", "kye kituufu", "bwe kiri", "yee kituufu",
        "ndiyo", "ndio", "sawa", "ni sawa", "naam", "ndiyo sawa",
    };

    private static readonly HashSet<string> No = new(StringComparer.Ordinal)
    {
        "no", "n", "nope", "nah", "wrong", "not right", "thats wrong", "that is wrong", "incorrect",
        "no its wrong", "no wrong", "2",
        "nedda", "si kituufu", "sikyo", "nedda si kituufu",
        "hapana", "si sawa", "hapana si sawa",
    };

    private static readonly HashSet<string> NewIncident = new(StringComparer.Ordinal)
    {
        "new", "new one", "a new one", "yes new", "its new", "it is new", "different", "different one",
        "another", "another one", "new accident", "a new accident", "different accident", "yes its new",
        "yes different", "new claim", "a new claim",
        "empya", "kipya", "ekirala", "kirala", "mpya", "ingine", "nyingine", "ajali mpya",
    };

    public static bool IsYes(string original, string english) => Matches(Yes, original) || Matches(Yes, english);

    public static bool IsNo(string original, string english) => Matches(No, original) || Matches(No, english);

    private static readonly HashSet<string> YesOpeners = new(StringComparer.Ordinal)
    {
        "yes", "yeah", "yep", "yup", "ok", "okay", "correct", "sure", "exactly", "right", "confirm", "confirmed",
        "yee", "ye", "yego", "kale", "kituufu", "ndiyo", "ndio", "sawa", "naam",
    };

    private static readonly string[] Retractions = ["not", "wrong", "change", "incorrect", "nedda", "hapana", "si kituufu"];

    /// <summary>A reply that opens with a clear yes ("yes that's right", "yee kituufu nnyo") and takes
    /// nothing back. Used to confirm a claim the customer has just had read back to them.</summary>
    public static bool StartsWithYes(string original, string english) => OpensWithYes(original) || OpensWithYes(english);

    private static bool OpensWithYes(string text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return false;
        }

        if (Yes.Contains(normalized))
        {
            return true;
        }

        var words = normalized.Split(' ');
        return YesOpeners.Contains(words[0])
            && words.Length <= 12
            && !Retractions.Any(retraction => $" {normalized} ".Contains($" {retraction} ", StringComparison.Ordinal));
    }

    public static bool IsNewIncident(string original, string english) =>
        Matches(NewIncident, original) || Matches(NewIncident, english);

    private static bool Matches(HashSet<string> words, string text)
    {
        var normalized = Normalize(text);
        return normalized.Length > 0 && words.Contains(normalized);
    }

    private static string Normalize(string text)
    {
        var letters = new string((text ?? string.Empty).ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ')
            .ToArray());
        return string.Join(' ', letters.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
