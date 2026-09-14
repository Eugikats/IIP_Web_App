namespace InsuranceIntelligence.Application.CommandCenter;

public enum IntentType
{
    ShowSlaBreaches,
    ShowAttentionClaims,
    ShowMissingDocuments,
    ShowPendingApprovals,
    GenerateManagementSummary,
    PrepareCustomerUpdateDraft,
    AnswerPolicyQuestion,

    /// <summary>Instructions rather than questions: see CommandCenterActionIntentParser.</summary>
    DraftCustomerMessage,
    AssignClaim,
    SummarizeClaim,

    /// <summary>A greeting, a thank-you, a goodbye, or "what can you do?". Not an operational
    /// query and not a policy question -- it needs an answer in kind, not a claims briefing.</summary>
    Conversational,
    Unsupported
}

/// <summary>Which of the four conversational openers was recognised, so the reply can answer the
/// thing that was actually said rather than greeting someone who just said thank you.</summary>
public static class ConversationalKind
{
    public const string Key = "conversationalKind";
    public const string Greeting = "greeting";
    public const string Thanks = "thanks";
    public const string Farewell = "farewell";
    public const string Capabilities = "capabilities";
}

public sealed record IntentClassificationResult(
    IntentType Intent,
    IntentParameters Parameters,
    decimal Confidence,
    bool UsedAiClassifier);

public sealed class IntentParameters
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    public IntentParameters(IReadOnlyDictionary<string, string?>? values = null)
    {
        _values = values ?? new Dictionary<string, string?>();
    }

    public bool TryGetString(string key, out string? value) => _values.TryGetValue(key, out value);

    public string? GetStringOrDefault(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyDictionary<string, string?> AsReadOnly() => _values;
}

public interface IIntentClassifier
{
    Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed class DeterministicThenAiIntentClassifier : IIntentClassifier
{
    public Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassifyDeterministically(message));

    private static IntentClassificationResult ClassifyDeterministically(string message)
    {
        var normalized = (message ?? string.Empty).Trim().ToLowerInvariant();

        // Checked first, and only ever against the whole message. Someone typing "hey" is not
        // asking for a workload briefing, and answering one with a claims count reads as an
        // assistant that cannot hear. Whole-message matching is what makes putting this first
        // safe: "hey, which claims are overdue?" is an operational query that merely opens with
        // a greeting, and still routes to the queue below.
        var conversational = NormalizeForConversation(normalized);
        if (conversational.Length > 0)
        {
            if (Greetings.Contains(conversational)) return Conversational(ConversationalKind.Greeting);
            if (Thanks.Contains(conversational)) return Conversational(ConversationalKind.Thanks);
            if (Farewells.Contains(conversational)) return Conversational(ConversationalKind.Farewell);
            if (CapabilityQuestions.Contains(conversational)) return Conversational(ConversationalKind.Capabilities);
        }

        // Instructions ("send a message to Eugene", "assign CLM-726E3445 to me") before any of the
        // question intents below, which would otherwise read "message" or "claim" as a query.
        if (CommandCenterActionIntentParser.TryParse(message, out var actionIntent))
        {
            return actionIntent;
        }

        // Vocabulary that only ever appears in a policy-knowledge question, never in an
        // operational queue query. Checked first so these route to grounded retrieval + AI
        // rather than falling through to an operational intent that happens to share a word.
        if (Mentions(normalized,
            "exclusion", "excluded", "waiting period", "sub-limit", "sublimit",
            "deductible", "excess", "policy wording", "co-payment", "copayment",
            "grace period", "premium", "sum insured", "sum assured", "what is covered",
            "what does the policy", "does the policy", "covered under", "benefit"))
        {
            return Result(IntentType.AnswerPolicyQuestion, 0.86m);
        }

        if (Mentions(normalized, "customer update", "customer communication", "draft update", "draft email", "draft message"))
        {
            return Result(IntentType.PrepareCustomerUpdateDraft, 0.88m, ExtractGuidParameter(message));
        }

        if (Mentions(normalized, "management summary", "executive summary", "management report", "operations summary"))
        {
            return Result(IntentType.GenerateManagementSummary, 0.88m);
        }

        if (Mentions(normalized, "approval", "approve", "sign off", "sign-off"))
        {
            return Result(IntentType.ShowPendingApprovals, 0.9m);
        }

        if (Mentions(normalized, "document", "missing", "incomplete", "checklist", "evidence pack"))
        {
            return Result(IntentType.ShowMissingDocuments, 0.9m);
        }

        if (Mentions(normalized, "sla", "breach", "overdue", "at risk", "at-risk", "due", "deadline"))
        {
            return Result(IntentType.ShowSlaBreaches, 0.9m);
        }

        if (Mentions(normalized, "claim", "case", "workload", "queue", "attention", "today"))
        {
            return Result(IntentType.ShowAttentionClaims, 0.85m);
        }

        // Nothing operational matched. If the user actually asked something (rather than
        // greeting or typing a fragment), send it to grounded retrieval + AI instead of
        // silently returning the standing workspace briefing — answering "how does X work?"
        // with an unrelated claims summary reads as a broken assistant.
        if (LooksLikeAQuestion(normalized))
        {
            return Result(IntentType.AnswerPolicyQuestion, 0.6m);
        }

        return Result(IntentType.Unsupported, 0m);
    }

    // Whole-message sets, deliberately not substring patterns. Each entry is what a person
    // actually types on its own line in a chat box; anything longer is a real request.
    private static readonly HashSet<string> Greetings = new(StringComparer.Ordinal)
    {
        "hi", "hii", "hiii", "hey", "heyy", "heya", "hello", "helo", "hiya", "yo", "sup",
        "hi there", "hey there", "hello there", "hi all", "hello all", "hi team", "hello team",
        "good morning", "good afternoon", "good evening", "good day",
        "morning", "afternoon", "evening", "greetings", "howdy", "hola",
        "how are you", "how are you doing", "how are you today", "hows it going",
        "how is it going", "how do you do", "whats up", "you there", "are you there",
    };

    private static readonly HashSet<string> Thanks = new(StringComparer.Ordinal)
    {
        "thanks", "thank you", "thanx", "thx", "ty", "cheers", "ta",
        "thanks a lot", "thanks so much", "thank you very much", "thank you so much",
        "much appreciated", "appreciated", "i appreciate it", "nice one",
        "ok thanks", "okay thanks", "great thanks", "perfect thanks", "brilliant thanks",
        "got it thanks", "that helps", "that helps thanks", "very helpful",
    };

    private static readonly HashSet<string> Farewells = new(StringComparer.Ordinal)
    {
        "bye", "byebye", "goodbye", "good bye", "see you", "see you later", "see ya", "later",
        "good night", "goodnight", "night", "thats all", "that is all", "thats all thanks",
        "nothing else", "no thanks", "im done", "i am done", "we are done", "were done",
    };

    private static readonly HashSet<string> CapabilityQuestions = new(StringComparer.Ordinal)
    {
        "help", "help me", "what can you do", "what can you help with", "what do you do",
        "what can i ask", "what can i ask you", "what should i ask", "how do i use this",
        "how does this work", "what is this", "who are you", "what are you", "options",
    };

    private const char StraightApostrophe = '\u0027';
    private const char CurlyApostrophe = '\u2019';

    /// <summary>Letters and digits only, single-spaced, with apostrophes removed rather than
    /// turned into gaps -- so "how's it going?!" and "Hows it going" both reach the same key.</summary>
    private static string NormalizeForConversation(string lowered)
    {
        var builder = new System.Text.StringBuilder(lowered.Length);
        var pendingSpace = false;
        foreach (var character in lowered)
        {
            if (character == StraightApostrophe || character == CurlyApostrophe)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(character);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    private static IntentClassificationResult Conversational(string kind) =>
        Result(IntentType.Conversational, 0.95m, new Dictionary<string, string?> { [ConversationalKind.Key] = kind });

    private static readonly string[] QuestionOpeners =
        ["what", "how", "why", "when", "which", "who", "where", "does", "do ", "can ", "is ", "are ", "should", "explain", "tell me"];

    private static bool LooksLikeAQuestion(string normalized) =>
        normalized.Length > 8
        && (normalized.EndsWith('?')
            || QuestionOpeners.Any(opener => normalized.StartsWith(opener, StringComparison.Ordinal)));

    private static IntentClassificationResult Result(
        IntentType intent,
        decimal confidence,
        IReadOnlyDictionary<string, string?>? parameters = null) =>
        new(intent, new IntentParameters(parameters), confidence, UsedAiClassifier: false);

    private static bool Mentions(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static IReadOnlyDictionary<string, string?> ExtractGuidParameter(string? message)
    {
        foreach (var token in (message ?? string.Empty)
            .Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token, out var value))
            {
                return new Dictionary<string, string?> { ["claimId"] = value.ToString() };
            }
        }

        return new Dictionary<string, string?>();
    }
}
