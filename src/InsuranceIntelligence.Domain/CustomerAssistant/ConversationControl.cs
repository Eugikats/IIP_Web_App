namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>Who is answering a WhatsApp conversation.</summary>
public enum ConversationMode
{
    /// <summary>Kato answers, as he always has.</summary>
    Kato,

    /// <summary>A named person has taken the conversation. Kato stays silent, but every message the
    /// customer sends is still recorded and still feeds the prospect's insights.</summary>
    Person,
}

/// <summary>
/// Whose conversation it is, worked out from the conversation's own record (a person's take-over, a
/// person's reply, a hand-back) rather than a flag that could drift from it. A person who stops
/// replying does not leave a customer unanswered for ever: Kato takes the conversation back after the
/// idle period.
/// </summary>
public static class ConversationControl
{
    /// <summary>How long a person can go without replying before Kato answers again.</summary>
    public static readonly TimeSpan IdleHandBack = TimeSpan.FromHours(2);

    public static ConversationMode ModeOf(IEnumerable<CustomerConversationTurn> turns, DateTimeOffset now) =>
        LastControlTurn(turns) is { } latest
        && latest.Outcome != CustomerAssistantOutcome.HandedBackToKato
        && now - latest.OccurredAt <= IdleHandBack
            ? ConversationMode.Person
            : ConversationMode.Kato;

    /// <summary>The newest turn that changed or exercised control, or null when nobody ever did.</summary>
    public static CustomerConversationTurn? LastControlTurn(IEnumerable<CustomerConversationTurn> turns) =>
        turns
            .Where(turn => turn.Outcome is CustomerAssistantOutcome.HumanTookOver
                or CustomerAssistantOutcome.HumanReplied
                or CustomerAssistantOutcome.HandedBackToKato)
            .OrderByDescending(turn => turn.OccurredAt)
            .FirstOrDefault();
}
