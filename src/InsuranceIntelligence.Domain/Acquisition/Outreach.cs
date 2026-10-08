namespace InsuranceIntelligence.Domain.Acquisition;

public enum OutreachKind
{
    Introduction,
    QuietFollowUp,
    PolicyEnding,
}

public enum OutreachStatus
{
    /// <summary>Written by the drafting agent. Nobody has looked at it yet.</summary>
    Draft,

    /// <summary>A person edited the wording. Still needs approval.</summary>
    Edited,

    /// <summary>A person approved and the send went to WhatsApp.</summary>
    Sent,

    /// <summary>A person approved but it could not go out, and the reason is on the draft.</summary>
    Blocked,

    /// <summary>Approved in the demonstration: recorded on the demo number, never delivered to a phone.</summary>
    DemoSent,

    /// <summary>A person chose not to send it.</summary>
    Dismissed,
}

/// <summary>A message prepared for one person. It is only ever a proposal: nothing is sent until a
/// named person approves it, and the wording they approve is the wording that goes out.</summary>
public sealed record OutreachDraft(
    Guid DraftId,
    OutreachKind Kind,
    string Text,
    string? Product,
    string? Insurer,
    OutreachStatus Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? DecidedAt = null,
    string? DecidedBy = null,
    string? BlockedReason = null,
    string? SentMessageId = null);

public static class OutreachDrafting
{
    public const string OptOut = "Reply STOP at any time and we will not message you again.";

    /// <summary>
    /// Writes the introduction for one person from what is recorded about them: their first name, what
    /// they do and why the cover suits them, the cover in the insurer's own words (with its price and
    /// the insurer named) and a way to say no. No claim is made that is not in the record or in the
    /// insurer's summary, and no sensitive attribute is ever used.
    /// </summary>
    public static string Introduction(string organisation, string customerName, string? reason, string coverExplanation, string insurer)
    {
        var first = FirstName(customerName);
        var why = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"{reason.Trim()} ";
        return $"Hello {first}, this is {organisation}'s insurance team. {why}{coverExplanation.Trim()} The cover is provided by {insurer}. " +
               $"Would you like me to explain how it works? {OptOut}";
    }

    public static string QuietFollowUp(string organisation, string customerName, string product, string insurer)
    {
        var first = FirstName(customerName);
        return $"Hello {first}, this is {organisation}'s insurance team. We spoke recently about {product} from {insurer}. " +
               $"Would you like to carry on, or is there something we could explain better? {OptOut}";
    }

    public static string PolicyEnding(string organisation, string customerName, string product, DateOnly endsOn)
    {
        var first = FirstName(customerName);
        return $"Hello {first}, this is {organisation}'s insurance team. Your {product} cover runs until {endsOn:d MMMM yyyy}. " +
               $"Would you like to renew it? {OptOut}";
    }

    public static string FirstName(string customerName)
    {
        var first = (customerName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? "there" : first;
    }

    /// <summary>
    /// The one line of a recommendation's reasons that reads well to the customer: their work, or that they
    /// bank with the bank. Other reasons (family situation, interest) are for staff, not for the message.
    /// </summary>
    public static string? CustomerReason(IEnumerable<string> reasons)
    {
        var list = reasons.ToArray();
        var work = list.FirstOrDefault(reason => reason.StartsWith("Works as ", StringComparison.OrdinalIgnoreCase));
        if (work is not null)
        {
            var role = work["Works as ".Length..];
            var cut = role.IndexOf(", which this cover is for", StringComparison.OrdinalIgnoreCase);
            if (cut > 0)
            {
                role = role[..cut];
            }

            // "boutique owner, boutique in Entebbe" keeps only the first part.
            role = role.Split(',')[0].Trim();
            return $"As a {role.ToLowerInvariant()}, this cover may suit you.";
        }

        return list.Any(reason => reason.StartsWith("Holds ", StringComparison.OrdinalIgnoreCase))
            ? "As someone who banks with us, this cover may suit you."
            : null;
    }
}
