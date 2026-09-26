namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// Lets the WhatsApp assistant carry context from the customer's earlier messages. Without it
/// every message was classified on its own, so the natural exchange "I had an accident" /
/// "please reply with your policy number" / "NSA/MO/2026/00033" lost the claim at the third
/// message: a bare policy number matches no intent keyword and was escalated as Unknown.
///
/// Deliberately narrow and deterministic. Only a turn where the assistant itself asked a question
/// (for a policy number, for which cover, or for what happened) is continued, only within a short
/// window, and a message that clearly asks for something else (a person, a claim status, a quote)
/// always wins. Identity rules are unchanged: a claim status is still disclosed only after the
/// sender's phone number or a typed policy number matches.
/// </summary>
public static class ConversationContinuation
{
    /// <summary>How long a question the assistant asked stays open.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    private static readonly string[] Greetings =
        ["hi", "hello", "hey", "hallo", "good morning", "good afternoon", "good evening", "greetings",
         "oli otya", "gyebale ko", "habari", "jambo", "mambo", "salaam"];

    /// <summary>Single-turn form, kept for callers that only have the previous turn.</summary>
    public static ContinuationResult Resolve(
        CustomerIntent classifiedIntent,
        bool messageHasPolicyNumber,
        CustomerConversationTurn? previousTurn,
        DateTimeOffset now) =>
        Resolve(classifiedIntent, messageHasPolicyNumber, previousTurn is null ? [] : [previousTurn], now);

    /// <summary>The intent to act on, the customer's earlier words in the same request (so a new
    /// claim keeps their original description of what happened), and the policy already chosen
    /// for it, if any.</summary>
    /// <param name="thread">This conversation's turns, oldest first.</param>
    public static ContinuationResult Resolve(
        CustomerIntent classifiedIntent,
        bool messageHasPolicyNumber,
        IReadOnlyList<CustomerConversationTurn> thread,
        DateTimeOffset now)
    {
        var previous = thread.Count == 0 ? null : thread[^1];
        if (previous is null || now - previous.OccurredAt > Window)
        {
            return new ContinuationResult(classifiedIntent, null);
        }

        // The assistant asked which cover, or what happened: the reply answers that question in
        // whatever words the customer uses ("the car one", "a taxi hit me at the roundabout"),
        // unless they clearly ask for something else instead.
        if (previous.Outcome is CustomerAssistantOutcome.AwaitingIncidentDetails or CustomerAssistantOutcome.AwaitingPolicyChoice)
        {
            return classifiedIntent is CustomerIntent.HumanRequested or CustomerIntent.ExistingClaimStatus or CustomerIntent.ProductInterest
                ? new ContinuationResult(classifiedIntent, null)
                : new ContinuationResult(CustomerIntent.NewClaim, EarlierClaimText(thread), previous.PolicyNumber);
        }

        var inClaimFlow = previous.ClassifiedIntent == CustomerIntent.NewClaim
            && previous.Outcome is CustomerAssistantOutcome.AwaitingMoreInformation or CustomerAssistantOutcome.Declined;
        if (inClaimFlow && classifiedIntent == CustomerIntent.NewClaim)
        {
            return new ContinuationResult(CustomerIntent.NewClaim, EarlierClaimText(thread), previous.PolicyNumber);
        }

        var carriesOwnIntent = classifiedIntent is not (CustomerIntent.Unknown or CustomerIntent.GeneralQuestion);
        if (carriesOwnIntent)
        {
            return new ContinuationResult(classifiedIntent, null);
        }

        // A follow-up question without a policy number is a new question, not the answer to ours.
        if (classifiedIntent == CustomerIntent.GeneralQuestion && !messageHasPolicyNumber)
        {
            return new ContinuationResult(classifiedIntent, null);
        }

        var awaitingPolicyNumber = previous.Outcome == CustomerAssistantOutcome.AwaitingMoreInformation
            && previous.ClassifiedIntent is CustomerIntent.NewClaim or CustomerIntent.ExistingClaimStatus;
        var retryingUnknownPolicy = previous.Outcome == CustomerAssistantOutcome.Declined
            && previous.ClassifiedIntent == CustomerIntent.NewClaim
            && messageHasPolicyNumber;

        if (!awaitingPolicyNumber && !retryingUnknownPolicy)
        {
            return new ContinuationResult(classifiedIntent, null);
        }

        var earlier = previous.ClassifiedIntent == CustomerIntent.NewClaim ? EarlierClaimText(thread) : previous.InboundMessageText;
        return new ContinuationResult(previous.ClassifiedIntent, earlier, previous.PolicyNumber);
    }

    /// <summary>A short opening message ("Hi", "Good morning", "Oli otya") that deserves a
    /// welcome and a list of what the assistant can do, not an escalation to a person.</summary>
    public static bool IsGreeting(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return false;
        }

        var normalized = new string(messageText.ToLowerInvariant()
            .Select(c => char.IsLetter(c) || char.IsWhiteSpace(c) ? c : ' ')
            .ToArray()).Trim();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length > 5)
        {
            return false;
        }

        var joined = string.Join(' ', words);
        return Greetings.Any(greeting => joined == greeting || joined.StartsWith(greeting + " ", StringComparison.Ordinal));
    }

    // Everything the customer has said in the claim request still open at the end of the thread,
    // oldest first, so the claim description is theirs in full rather than only the last reply.
    private static string EarlierClaimText(IReadOnlyList<CustomerConversationTurn> thread)
    {
        var texts = new List<string>();
        for (var index = thread.Count - 1; index >= 0; index--)
        {
            var turn = thread[index];
            var partOfClaimRequest = turn.ClassifiedIntent == CustomerIntent.NewClaim
                && turn.Outcome is CustomerAssistantOutcome.AwaitingIncidentDetails
                    or CustomerAssistantOutcome.AwaitingPolicyChoice
                    or CustomerAssistantOutcome.AwaitingMoreInformation
                    or CustomerAssistantOutcome.Declined;
            if (!partOfClaimRequest)
            {
                break;
            }

            texts.Add(turn.InboundMessageText);
        }

        texts.Reverse();
        return string.Join("\n", texts);
    }
}

public sealed record ContinuationResult(CustomerIntent Intent, string? EarlierMessageText, string? PolicyNumber = null);
