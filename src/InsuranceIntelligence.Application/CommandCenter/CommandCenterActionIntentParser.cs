using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Application.CommandCenter;

/// <summary>
/// Recognises the three things a person can ask the assistant to do, as opposed to ask it about:
/// draft a message to a customer, assign a claim, and run the claims agent on a claim.
///
/// Before this, "send a message to Eugene" classified as nothing and fell through to the workspace
/// briefing, so the assistant answered an instruction with a count of claims. For a product sold as
/// agentic, a chat box that cannot act on a plain instruction undermines everything else it does.
///
/// Every pattern is anchored to the whole message, and the target has to look like a person's name
/// or a claim reference. That second check is what keeps "summarise claims missing documents" or
/// "message the team" from being read as an instruction about a customer called "claims missing
/// documents"; those still reach the operational intents below this parser.
/// </summary>
public static partial class CommandCenterActionIntentParser
{
    public const string TargetKey = "target";
    public const string AssigneeKey = "assignee";
    public const string InstructionKey = "instruction";

    // Words that mean the request is about a queue or a group, never one customer's claim.
    private static readonly HashSet<string> NonTargetWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "claims", "claim", "cases", "documents", "document", "approvals", "approval", "sla", "slas", "today",
        "overdue", "missing", "pending", "all", "every", "my", "open", "team", "everyone", "customers",
        "policy", "policies", "queue", "workload", "summary", "report", "breaches", "late", "urgent",
    };

    private static readonly HashSet<string> Pronouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "me", "us", "myself", "him", "her", "them", "it", "you", "i",
    };

    public static bool TryParse(string? message, out IntentClassificationResult result)
    {
        result = default!;
        var text = Normalize(message);
        if (text.Length < 6)
        {
            return false;
        }

        foreach (var pattern in AssignPatterns)
        {
            var match = pattern.Match(text);
            if (!match.Success)
            {
                continue;
            }

            var target = CleanTarget(match.Groups["target"].Value);
            var assignee = CleanPerson(match.Groups["assignee"].Value);
            if (LooksLikeClaimTarget(target) && assignee.Length > 0)
            {
                result = Build(IntentType.AssignClaim, (TargetKey, target), (AssigneeKey, assignee));
                return true;
            }
        }

        foreach (var pattern in SummarizePatterns)
        {
            var match = pattern.Match(text);
            if (!match.Success)
            {
                continue;
            }

            var target = CleanTarget(match.Groups["target"].Value);
            if (LooksLikeClaimTarget(target))
            {
                result = Build(IntentType.SummarizeClaim, (TargetKey, target));
                return true;
            }
        }

        foreach (var pattern in DraftPatterns)
        {
            var match = pattern.Match(text);
            if (!match.Success)
            {
                continue;
            }

            var target = CleanTarget(match.Groups["target"].Value);
            if (!LooksLikeClaimTarget(target))
            {
                continue;
            }

            var instruction = match.Groups["instruction"].Success ? match.Groups["instruction"].Value.Trim() : string.Empty;
            result = Build(IntentType.DraftCustomerMessage, (TargetKey, target), (InstructionKey, instruction));
            return true;
        }

        return false;
    }

    private static readonly Regex[] AssignPatterns =
    [
        new(@"^(?:please\s+)?(?:can you\s+|could you\s+)?(?:re)?assign\s+(?:the\s+)?(?:claim\s+|case\s+)?(?:for\s+|of\s+|belonging to\s+|from\s+)?(?<target>.+?)\s+to\s+(?<assignee>[^,]+?)$", Options),
        new(@"^(?:please\s+)?(?:give|hand|pass|route|move)\s+(?<target>.+?)\s+(?:claim|case)\s+to\s+(?<assignee>[^,]+?)$", Options),
    ];

    private static readonly Regex[] SummarizePatterns =
    [
        new(@"^(?:please\s+)?(?:can you\s+|could you\s+)?(?:summari[sz]e|review|triage|analy[sz]e|assess)\s+(?:the\s+)?(?:claim|case)\s+(?:for\s+|of\s+|from\s+|belonging to\s+)?(?<target>.+)$", Options),
        new(@"^(?:please\s+)?(?:can you\s+|could you\s+)?(?:summari[sz]e|review|triage|analy[sz]e|assess)\s+(?<target>.+?)(?:'s|’s)\s+(?:claim|case)$", Options),
        new(@"^(?:please\s+)?(?:summari[sz]e|review|triage|analy[sz]e|assess)\s+(?<target>CLM-[0-9a-f]{8})$", Options),
        new(@"^(?:give me\s+|show me\s+|get me\s+)?(?:a\s+)?(?:quick\s+)?summary\s+(?:of|for)\s+(?:the\s+)?(?:claim\s+(?:for|of)\s+)?(?<target>.+?)(?:(?:'s|’s)\s+claim|\s+claim)?$", Options),
        new(@"^(?:please\s+)?run\s+the\s+claims?\s+agent\s+(?:on|for)\s+(?<target>.+)$", Options),
    ];

    private static readonly Regex[] DraftPatterns =
    [
        new(@"^(?:please\s+)?(?:can you\s+|could you\s+)?(?:send|write|draft|prepare|compose)\s+(?:a\s+|an\s+)?(?:quick\s+|short\s+)?(?:message|update|note|email|sms|text|reply)\s+(?:to|for|about)\s+(?<target>.+?)(?:\s+(?:saying|to say|that says|telling (?:him|her|them)|to tell (?:him|her|them)|about|regarding|asking(?: (?:him|her|them))?(?: to| for)?)\s+(?<instruction>.+))?$", Options),
        new(@"^(?:please\s+)?(?:message|text|email|sms|notify|contact)\s+(?<target>.+?)(?:\s+(?:saying|to say|that|about|regarding|asking)\s+(?<instruction>.+))?$", Options),
        new(@"^(?:please\s+)?(?:send|give)\s+(?<target>.+?)\s+(?:a|an)\s+(?:quick\s+|short\s+)?(?:message|update|text|email|note)(?:\s+(?:saying|about|that|regarding)\s+(?<instruction>.+))?$", Options),
        new(@"^(?:please\s+)?let\s+(?<target>.+?)\s+know\s+(?:that\s+|about\s+)?(?<instruction>.+)$", Options),
        new(@"^(?:please\s+)?update\s+(?<target>.+?)\s+(?:on|about|regarding)\s+(?:their|his|her|the)\s+claim(?:\s+(?:saying|that)\s+(?<instruction>.+))?$", Options),
    ];

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static string Normalize(string? message)
    {
        var text = WhitespaceRun().Replace((message ?? string.Empty).Trim(), " ");
        return text.TrimEnd('.', '?', '!', ' ');
    }

    private static string CleanTarget(string value)
    {
        var text = value.Trim().Trim('"', '\'', '“', '”');
        text = LeadingNoise().Replace(text, string.Empty);
        text = TrailingNoise().Replace(text, string.Empty);
        return text.Trim();
    }

    private static string CleanPerson(string value)
    {
        var text = value.Trim().Trim('"', '\'');
        text = TrailingPolite().Replace(text, string.Empty);
        return text.Trim();
    }

    private static bool LooksLikeClaimTarget(string target)
    {
        if (target.Length == 0)
        {
            return false;
        }

        if (ClaimReference.FindIn(target).Count > 0)
        {
            return true;
        }

        var words = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > 4)
        {
            return false;
        }

        return words.All(word => NameWord().IsMatch(word))
            && !words.Any(NonTargetWords.Contains)
            && !(words.Length == 1 && Pronouns.Contains(words[0]));
    }

    private static IntentClassificationResult Build(IntentType intent, params (string Key, string Value)[] parameters) =>
        new(
            intent,
            new IntentParameters(parameters.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)),
            0.9m,
            UsedAiClassifier: false);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"^(?:the\s+)?(?:customer|claimant|policyholder|client|insured)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingNoise();

    [GeneratedRegex(@"(?:(?:'s|’s)?\s+(?:claim|case))$|(?:'s|’s)$|\s+please$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingNoise();

    [GeneratedRegex(@"\s+please$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingPolite();

    [GeneratedRegex(@"^[\p{L}][\p{L}'’.-]*$")]
    private static partial Regex NameWord();
}
