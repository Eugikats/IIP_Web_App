namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// Lets the WhatsApp assistant carry one step of context from the customer's previous message.
/// Without it every message was classified on its own, so the natural exchange "I had an
/// accident" / "please reply with your policy number" / "NSA/MO/2026/00033" lost the claim at the
/// third message: a bare policy number matches no intent keyword and was escalated as Unknown.
///
/// Deliberately narrow and deterministic. Only a turn where the assistant itself asked for more
/// information (or could not find the policy number it was given for a new claim) is continued,
/// only within a short window, and only when the new message does not carry a clear intent of
/// its own. Identity rules are unchanged: a claim status is still disclosed only after a policy
/// number match on the new message.
/// </summary>
public static class ConversationContinuation
{
    /// <summary>How long a "please reply with your policy number" stays open.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    private static readonly string[] Greetings =
        ["hi", "hello", "hey", "hallo", "good morning", "good afternoon", "good evening", "greetings",
         "oli otya", "gyebale ko", "habari", "jambo", "mambo", "salaam"];

    /// <summary>The intent to act on, and the previous message's text when the intent was carried
    /// over from it (so a new claim keeps the customer's original description of what happened).</summary>
    public static ContinuationResult Resolve(
        CustomerIntent classifiedIntent,
        bool messageHasPolicyNumber,
        CustomerConversationTurn? previousTurn,
        DateTimeOffset now)
    {
        var carriesOwnIntent = classifiedIntent is not (CustomerIntent.Unknown or CustomerIntent.GeneralQuestion);
        if (carriesOwnIntent || previousTurn is null || now - previousTurn.OccurredAt > Window)
        {
            return new ContinuationResult(classifiedIntent, null);
        }

        // A follow-up question without a policy number is a new question, not the answer to ours.
        if (classifiedIntent == CustomerIntent.GeneralQuestion && !messageHasPolicyNumber)
        {
            return new ContinuationResult(classifiedIntent, null);
        }

        var awaitingPolicyNumber = previousTurn.Outcome == CustomerAssistantOutcome.AwaitingMoreInformation
            && previousTurn.ClassifiedIntent is CustomerIntent.NewClaim or CustomerIntent.ExistingClaimStatus;
        var retryingUnknownPolicy = previousTurn.Outcome == CustomerAssistantOutcome.Declined
            && previousTurn.ClassifiedIntent == CustomerIntent.NewClaim
            && messageHasPolicyNumber;

        return awaitingPolicyNumber || retryingUnknownPolicy
            ? new ContinuationResult(previousTurn.ClassifiedIntent, previousTurn.InboundMessageText)
            : new ContinuationResult(classifiedIntent, null);
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
}

public sealed record ContinuationResult(CustomerIntent Intent, string? EarlierMessageText);
