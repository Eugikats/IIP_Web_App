using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// Small, deterministic readings of a customer's WhatsApp message that let the assistant hold a
/// natural conversation: a "thanks" is answered warmly instead of being escalated, "are you a
/// bot?" gets an honest answer, "send me the link" gets the link, and a claim description is
/// checked for enough detail before a claim is opened.
///
/// Pure functions over text, so every rule is covered by tests and none of them is a decision
/// about the insurance matter itself. Deciding what to do with the result stays with the
/// assistant service and, for anything consequential, with a person.
/// </summary>
public static partial class CustomerMessageSignals
{
    // Every word of the message must come from this set, so "ok, thanks" closes the conversation
    // while "ok I had an accident" does not.
    private static readonly HashSet<string> CloseWords = new(StringComparer.Ordinal)
    {
        "thanks", "thank", "you", "u", "so", "much", "very", "a", "lot", "ok", "okay", "k", "kk",
        "alright", "noted", "done", "bye", "goodbye", "cool", "great", "perfect", "fine", "sure",
        "received", "got", "it", "thx", "ty", "webale", "nyo", "nnyo", "asante", "sana", "nice", "good",
    };

    private static readonly string[] AutomatedQuestions =
    [
        "are you a bot", "are you bot", "are you a robot", "are you human", "are you a human",
        "are you real", "are you a real person", "are you a person", "is this a bot", "is this a robot",
        "is this automated", "is this a machine", "am i talking to a bot", "am i talking to a robot",
        "am i talking to a machine", "am i talking to a person", "am i talking to a human",
        "am i chatting with a bot", "is this ai", "are you ai", "are you an ai",
    ];

    private static readonly string[] AccessLinkPhrases =
    [
        "link", "upload", "send documents", "send my documents", "submit documents", "add documents",
        "send the documents", "attach", "portal", "where do i send", "how do i send",
    ];

    private static readonly HashSet<string> BareHumanRequests = new(StringComparer.Ordinal)
    {
        "agent", "an agent", "human", "a human", "person", "a person", "operator", "customer care",
        "someone", "staff", "representative",
    };

    // Words that point at one product line. The line names themselves are included so a customer
    // answering "which cover?" with "motor" or "the medical one" is understood.
    private static readonly (string Line, string[] Words)[] LineWords =
    [
        ("Motor", ["motor", "car", "vehicle", "crash", "crashed", "collision", "windscreen", "bumper", "boda", "taxi", "driving", "drove", "parked", "parking", "tyre", "tire", "engine", "van", "lorry", "truck", "matatu", "bus", "rear-ended", "headlight"]),
        ("Medical", ["medical", "hospital", "clinic", "doctor", "admitted", "admission", "sick", "illness", "ill", "malaria", "surgery", "operation", "treatment", "prescription", "medicine", "pharmacy", "maternity", "dental", "tooth", "optical", "glasses", "lab", "scan"]),
        ("Life", ["life", "death", "died", "funeral", "burial", "deceased", "maturity", "endowment"]),
        ("General", ["general", "fire", "burglary", "burgled", "shop", "warehouse", "house", "home", "property", "cargo", "goods", "stock", "premises", "office"]),
        ("Agriculture", ["agriculture", "farm", "crop", "crops", "harvest", "livestock", "cow", "cows", "cattle", "goat", "goats", "poultry", "chicken", "chickens", "drought", "maize", "beans", "coffee"]),
        // Microinsurance lines (Kato field test).
        ("Hospital Cash", ["hospital", "admitted", "admission", "ward", "nights", "clinic", "sick", "illness", "ill", "malaria", "funeral", "burial", "died", "death", "ddwaliro"]),
        ("Boda Rider Accident", ["boda", "bodaboda", "rider", "accident", "injured", "injury", "hurt", "knocked", "crash", "crashed", "motorcycle", "motorbike", "fell", "akabenje", "kabenje", "ajali"]),
        ("Market Stall Stock", ["stall", "stock", "goods", "market", "shop", "fire", "burnt", "burned", "burglary", "burgled", "stolen", "thieves", "kiosk", "katale", "dduuka"]),
    ];

    private static readonly (string Line, string Phrase)[] LinePhrases =
    [
        ("Life", "passed away"),
        ("General", "broke into"),
        ("General", "break in"),
        ("Motor", "hit my car"),
        ("Motor", "knocked my"),
        ("Hospital Cash", "hospital cash"),
        ("Boda Rider Accident", "boda rider"),
        ("Boda Rider Accident", "rider accident"),
        ("Market Stall Stock", "market stall"),
        ("Market Stall Stock", "stall stock"),
    ];

    private static readonly string[] Weekdays = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    public static bool IsThanksOrClose(string text)
    {
        var words = Words(text);
        return words.Length is > 0 and <= 6 && words.All(CloseWords.Contains);
    }

    public static bool IsAskingIfAutomated(string text)
    {
        var normalized = Normalized(text);
        return AutomatedQuestions.Any(question => normalized.Contains(question, StringComparison.Ordinal));
    }

    public static bool IsAccessLinkRequest(string text)
    {
        var normalized = Normalized(text);
        return AccessLinkPhrases.Any(phrase => ContainsWordPhrase(normalized, phrase));
    }

    /// <summary>"Agent", "human", "a person" on its own: a request for a person that the keyword
    /// classifier's longer phrases ("speak to a human") do not catch.</summary>
    public static bool IsBareHumanRequest(string text) => BareHumanRequests.Contains(Normalized(text));

    /// <summary>The product line a message most clearly points at, or null when it points at none
    /// or at two lines equally.</summary>
    public static string? ProductLineHint(string text) => ProductLineHint(text, null);

    /// <summary>The same reading, limited to the given product lines (for example the customer's
    /// own covers), so a word shared with a line they do not hold cannot cause a tie.</summary>
    public static string? ProductLineHint(string text, IReadOnlyCollection<string>? candidateLines)
    {
        var normalized = Normalized(text);
        var words = Words(text);
        var scores = LineWords
            .Where(line => candidateLines is null || candidateLines.Contains(line.Line, StringComparer.OrdinalIgnoreCase))
            .Select(line => (line.Line, Score: words.Count(word => line.Words.Contains(word))
                + LinePhrases.Count(phrase => phrase.Line == line.Line && ContainsWordPhrase(normalized, phrase.Phrase))))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ToArray();

        if (scores.Length == 0 || (scores.Length > 1 && scores[0].Score == scores[1].Score))
        {
            return null;
        }

        return scores[0].Line;
    }

    /// <summary>A reply like "2", "option 1" or "number 3" to a numbered list.</summary>
    public static int? ChoiceNumber(string text)
    {
        var match = ChoicePattern().Match(Normalized(text));
        return match.Success ? int.Parse(match.Groups["n"].Value) : null;
    }

    /// <summary>Enough of a description for a claims officer to start from: at least seven words
    /// once any policy number is set aside. "I had an accident" is not; "I had an accident on
    /// Jinja Road this morning" is. The officer confirms every WhatsApp claim before it proceeds,
    /// so this only decides whether to ask one more question first.</summary>
    public static bool HasEnoughIncidentDetail(string text)
    {
        var withoutPolicyNumbers = PolicyNumberPattern().Replace(text ?? string.Empty, " ");
        return Words(withoutPolicyNumbers).Count(word => word.Any(char.IsLetter)) >= 7;
    }

    /// <summary>The incident date a description implies ("yesterday", "last night", "on
    /// Monday"), defaulting to today. Recorded as reported, and confirmed by the claims officer.</summary>
    public static DateOnly IncidentDate(string text, DateOnly today)
    {
        var normalized = Normalized(text);
        if (ContainsWordPhrase(normalized, "day before yesterday"))
        {
            return today.AddDays(-2);
        }

        if (ContainsWordPhrase(normalized, "yesterday") || ContainsWordPhrase(normalized, "last night"))
        {
            return today.AddDays(-1);
        }

        for (var index = 0; index < Weekdays.Length; index++)
        {
            if (ContainsWordPhrase(normalized, Weekdays[index]))
            {
                var daysBack = ((int)today.DayOfWeek - index + 7) % 7;
                return today.AddDays(-daysBack);
            }
        }

        return today;
    }

    private static string Normalized(string? text)
    {
        var lowered = (text ?? string.Empty).ToLowerInvariant();
        var cleaned = new string(lowered.Select(c => char.IsLetterOrDigit(c) || c is '-' ? c : ' ').ToArray());
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string[] Words(string? text) =>
        Normalized(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static bool ContainsWordPhrase(string normalized, string phrase) =>
        $" {normalized} ".Contains($" {phrase} ", StringComparison.Ordinal);

    [GeneratedRegex(@"^(?:option |number |no |choice )?(?<n>[1-9])$")]
    private static partial Regex ChoicePattern();

    [GeneratedRegex(@"[A-Za-z]{2,4}/[A-Za-z]{2}/\d{4}/\d{4,6}")]
    private static partial Regex PolicyNumberPattern();
}
