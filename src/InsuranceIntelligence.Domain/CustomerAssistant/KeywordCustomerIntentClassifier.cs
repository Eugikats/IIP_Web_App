namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// Deterministic, rule-based classification of an inbound customer message into the six-intent
/// set, matches the RuleBased implementation kind pattern already used for routing and
/// risk-fraud-signal (AgentCatalog), rather than adding a fourth live-untested LLM call site to a
/// codebase where every existing Azure OpenAI call site is genuinely load-bearing and worth
/// keeping to a minimum. Keyword order matters: more specific/urgent intents are checked first so
/// a message like "I want to make a claim, can I speak to someone" resolves to NewClaim, not
/// HumanRequested, see the test suite for the exact precedence this encodes.
/// </summary>
public sealed class KeywordCustomerIntentClassifier : ICustomerIntentClassifier
{
    private static readonly string[] NewClaimKeywords =
        ["accident", "crash", "collision", "make a claim", "file a claim", "start a claim", "damaged", "stolen", "theft", "fire damage", "was hit", "hit my car", "lodge a claim"];

    private static readonly string[] ExistingClaimStatusKeywords =
        ["status of my claim", "claim status", "update on my claim", "how is my claim", "track my claim", "my claim reference", "where is my claim", "progress on my claim"];

    private static readonly string[] ProductInterestKeywords =
        ["interested in", "want to buy", "get a quote", "how much is", "premium for", "pricing for", "sign up for", "want cover", "want insurance", "new policy"];

    private static readonly string[] HumanRequestedKeywords =
        ["speak to a human", "speak to someone", "talk to someone", "talk to a manager", "speak to a manager", "talk to an agent", "human agent", "real person", "customer service", "call me", "speak to a person"];

    public CustomerIntent Classify(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return CustomerIntent.Unknown;
        }

        var normalized = messageText.ToLowerInvariant();

        // Order matters: claim-related intents are checked before the more generic
        // human-requested/product-interest ones, so "I had an accident, please help" resolves to
        // NewClaim rather than a generic escalation.
        if (ContainsAny(normalized, NewClaimKeywords)) return CustomerIntent.NewClaim;
        if (ContainsAny(normalized, ExistingClaimStatusKeywords)) return CustomerIntent.ExistingClaimStatus;
        if (ContainsAny(normalized, ProductInterestKeywords)) return CustomerIntent.ProductInterest;
        if (ContainsAny(normalized, HumanRequestedKeywords)) return CustomerIntent.HumanRequested;

        // A real question (contains a question mark, or starts with a common question word) with
        // enough content to plausibly be answerable, otherwise Unknown, which escalates rather
        // than guessing.
        var looksLikeAQuestion = normalized.Contains('?')
            || StartsWithAny(normalized, "what", "how", "when", "where", "why", "can i", "does my", "is my", "do i", "am i");
        return looksLikeAQuestion && normalized.Trim().Length >= 6 ? CustomerIntent.GeneralQuestion : CustomerIntent.Unknown;
    }

    private static bool ContainsAny(string haystack, string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static bool StartsWithAny(string haystack, params string[] prefixes) =>
        prefixes.Any(prefix => haystack.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
}
