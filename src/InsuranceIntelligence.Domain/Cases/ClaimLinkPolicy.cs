using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>Why a claim link could not be sent, in words a claims officer can act on.</summary>
public enum ClaimLinkRefusal
{
    None,
    NoContact,
    ChannelHasNoContact,
    ConsentWithdrawn,
    ConsentRequired,
}

/// <summary>
/// The rules for sending a claimant the link to their own claim page (build-loop item 22): which
/// channel, whether the customer's consent allows it, what the message says, and how the address is
/// shown back to staff without exposing all of it. The link itself is single use and short lived; this
/// holds no secret.
/// </summary>
public static class ClaimLinkPolicy
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    /// <summary>The key a claim link message carries as its source, so the send path knows it is a link and
    /// never stores the link itself in a message record.</summary>
    public const string SourceKey = "claim-link";

    /// <summary>What is stored in place of the message when it carries the link.</summary>
    public const string RedactedBody = "A secure link to the claim was sent to the customer.";

    /// <summary>The channel and address to use: the one staff asked for if the claim has a contact for it,
    /// otherwise the claim's own channel (see <see cref="ClaimStatusNotificationPolicy.RouteFor"/>).</summary>
    public static (StatusMessageRoute? Route, ClaimLinkRefusal Refusal) Choose(string? requestedChannel, string claimChannel, string? phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(requestedChannel))
        {
            var route = ClaimStatusNotificationPolicy.RouteFor(claimChannel, phone, email);
            return route is null ? (null, ClaimLinkRefusal.NoContact) : (route, ClaimLinkRefusal.None);
        }

        if (!Enum.TryParse<CommunicationChannel>(requestedChannel.Trim(), ignoreCase: true, out var channel))
        {
            return (null, ClaimLinkRefusal.ChannelHasNoContact);
        }

        return channel switch
        {
            CommunicationChannel.SMS or CommunicationChannel.WhatsApp when !string.IsNullOrWhiteSpace(phone) => (new StatusMessageRoute(channel, phone.Trim()), ClaimLinkRefusal.None),
            CommunicationChannel.Email when !string.IsNullOrWhiteSpace(email) => (new StatusMessageRoute(channel, email.Trim()), ClaimLinkRefusal.None),
            _ => (null, ClaimLinkRefusal.ChannelHasNoContact),
        };
    }

    /// <summary>WhatsApp needs the customer's recorded, unwithdrawn consent. A text message or email about the
    /// customer's own claim does not need a fresh opt in, but it is never sent after the customer withdrew consent
    /// for that channel.</summary>
    public static ClaimLinkRefusal ConsentCheck(CommunicationChannel channel, ConsentRecord? current)
    {
        if (channel == CommunicationChannel.WhatsApp)
        {
            return ConsentEnforcementGuard.IsPermitted(current) ? ClaimLinkRefusal.None : ClaimLinkRefusal.ConsentRequired;
        }

        return current is not null && !ConsentEnforcementGuard.IsPermitted(current) ? ClaimLinkRefusal.ConsentWithdrawn : ClaimLinkRefusal.None;
    }

    public static string PurposeFor(CommunicationChannel channel) => channel switch
    {
        CommunicationChannel.WhatsApp => ConsentPurposes.WhatsApp,
        CommunicationChannel.Email => ConsentPurposes.Email,
        _ => ConsentPurposes.Sms,
    };

    public static string PlainReason(ClaimLinkRefusal refusal) => refusal switch
    {
        ClaimLinkRefusal.NoContact => "There is no phone number or email address on this claim to send a link to.",
        ClaimLinkRefusal.ChannelHasNoContact => "The claim has no contact for that channel.",
        ClaimLinkRefusal.ConsentWithdrawn => "The customer has withdrawn consent to be contacted on this channel.",
        ClaimLinkRefusal.ConsentRequired => "There is no recorded consent from the customer to message them on WhatsApp.",
        _ => string.Empty,
    };

    public static (string Body, string ShortBody) Compose(string customerName, string claimReference, string link)
    {
        var first = (customerName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var greeting = first.Length == 0 ? "Hello." : $"Hello {(first.Length > 40 ? first[..40] : first)}.";
        var body = $"{greeting} Use this secure link to see your claim {claimReference}, check what we still need and send your documents. It works once and for 24 hours: {link}";
        return (body, $"Claim {claimReference}: your secure link (works once, 24 hours): {link}");
    }

    /// <summary>The address with the middle hidden, so the screen confirms where it went without displaying all of it.</summary>
    public static string Mask(string recipient)
    {
        var value = (recipient ?? string.Empty).Trim();
        var at = value.IndexOf('@');
        if (at > 0)
        {
            return value[0] + "***" + value[at..];
        }

        var digits = value.Length;
        return digits <= 6 ? new string('*', digits) : value[..4] + new string('*', digits - 6) + value[^2..];
    }
}
