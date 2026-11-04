using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>A WhatsApp message template the insurer has had approved by Meta, to use for one status.
/// WhatsApp only allows a business to start a conversation, or to write after the customer's last message
/// is more than 24 hours old, with an approved template.</summary>
public sealed record WhatsAppTemplateRef(string Name, string Language = "en");

/// <summary>Where a status message goes: a channel and an address on that channel.</summary>
public sealed record StatusMessageRoute(CommunicationChannel Channel, string Recipient);

/// <summary>
/// The rules for telling a customer that their claim's status changed (build-loop item 21). The words
/// are the workspace's own wording for the status (item 19), so what is sent is what the insurer wrote
/// and approved there, never text a model made up. Whether the message may go without a person approving
/// it is the existing customer update policy; the statuses that announce an outcome always need a person.
/// </summary>
public static class ClaimStatusNotificationPolicy
{
    /// <summary>Statuses that do not send a message of their own: a new claim has the acknowledgement the
    /// platform already sends, and Waiting on documents sends the list of what is missing (the workflow
    /// rules' own request), which a generic status line would only duplicate.</summary>
    public static bool SendsMessage(CaseStatus status) => status is not (CaseStatus.New or CaseStatus.MissingDocuments);

    public static CustomerUpdateReasonCategory ReasonFor(CaseStatus status) => status switch
    {
        CaseStatus.ReadyForReview => CustomerUpdateReasonCategory.ReadyForReview,
        CaseStatus.Escalated => CustomerUpdateReasonCategory.EscalationReceived,
        CaseStatus.Settled or CaseStatus.Closed => CustomerUpdateReasonCategory.DecisionOutcome,
        _ => CustomerUpdateReasonCategory.StatusUpdate,
    };

    /// <summary>The key a status message carries as its source, so the WhatsApp template for it can be found.</summary>
    public static string SourceKey(CaseStatus status) => $"status:{status}";

    public static bool TryStatusFromSourceKey(string? sourceKey, out CaseStatus status)
    {
        status = default;
        return sourceKey is not null
            && sourceKey.StartsWith("status:", StringComparison.Ordinal)
            && Enum.TryParse(sourceKey["status:".Length..], ignoreCase: false, out status)
            && Enum.IsDefined(status);
    }

    /// <summary>True when the message may be sent without a person approving it: the workspace has turned
    /// that on, and the update policy does not require approval for this kind of message.</summary>
    public static bool MaySendWithoutApproval(bool autoSendEnabled, CaseStatus status) =>
        autoSendEnabled && !CustomerUpdateApprovalPolicy.RequiresApproval(sensitive: false, ReasonFor(status));

    /// <summary>The customer's own channel for the claim: WhatsApp when the claim came in on WhatsApp, otherwise
    /// a text message to the phone on the claim, otherwise email. Null when there is no way to reach them.</summary>
    public static StatusMessageRoute? RouteFor(string claimChannel, string? phone, string? email)
    {
        if (string.Equals(claimChannel, "WhatsApp", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(phone))
        {
            return new StatusMessageRoute(CommunicationChannel.WhatsApp, phone.Trim());
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            return new StatusMessageRoute(CommunicationChannel.SMS, phone.Trim());
        }

        return !string.IsNullOrWhiteSpace(email) ? new StatusMessageRoute(CommunicationChannel.Email, email.Trim()) : null;
    }

    /// <summary>The long and the short (text message) form of the message.</summary>
    public static (string Body, string ShortBody) Compose(StatusWording wording, string claimReference, string customerName)
    {
        var firstName = FirstName(customerName);
        var greeting = firstName.Length == 0 ? "Hello." : $"Hello {firstName}.";
        var body = $"{greeting} An update on your claim {claimReference}: {wording.Headline}. {wording.Detail}";
        var shortBody = $"Claim {claimReference}: {wording.Headline}.";
        return (body, shortBody.Length <= 160 ? shortBody : shortBody[..157] + "...");
    }

    private static string FirstName(string name)
    {
        var first = (name ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return first.Length > 40 ? first[..40] : first;
    }
}

/// <summary>How a send ended, in the delivery status words the customer update pipeline already uses.</summary>
public static class CustomerUpdateDelivery
{
    /// <summary>True for a delivery status that means the message did not go out.</summary>
    public static bool IsFailure(string? deliveryStatus) =>
        deliveryStatus is "ApprovalRequired" or "ConfigurationRequired" or "Failed" or "ConsentRequired" or "TemplateRequired";

    public static string PlainReason(string? deliveryStatus) => deliveryStatus switch
    {
        "ApprovalRequired" => "The message still needs a person to approve it.",
        "ConfigurationRequired" => "Sending is not configured for this channel or recipient.",
        "ConsentRequired" => "There is no recorded consent from the customer for this channel.",
        "TemplateRequired" => "The customer last wrote more than 24 hours ago, so WhatsApp only allows an approved message template, and none is set for this status.",
        "Failed" => "The provider did not accept the message.",
        _ => "The message could not be sent.",
    };
}

/// <summary>Checks the WhatsApp templates an administrator enters.</summary>
public static class WhatsAppTemplates
{
    public static IReadOnlyList<string> Validate(IReadOnlyDictionary<string, WhatsAppTemplateRef>? templates)
    {
        var errors = new List<string>();
        if (templates is null)
        {
            return errors;
        }

        foreach (var (key, value) in templates)
        {
            if (!Enum.TryParse<CaseStatus>(key, ignoreCase: false, out var status) || !Enum.IsDefined(status))
            {
                errors.Add($"{key} is not a claim status.");
                continue;
            }

            // Meta template names are lower case letters, digits and underscores.
            if (string.IsNullOrWhiteSpace(value.Name) || !System.Text.RegularExpressions.Regex.IsMatch(value.Name, "^[a-z0-9_]{1,512}$"))
            {
                errors.Add($"The WhatsApp template name for {status} must be lower case letters, digits and underscores, as Meta approved it.");
            }

            if (string.IsNullOrWhiteSpace(value.Language) || !System.Text.RegularExpressions.Regex.IsMatch(value.Language, "^[a-z]{2,3}(_[A-Z]{2})?$"))
            {
                errors.Add($"The WhatsApp template language for {status} must look like en or en_GB.");
            }
        }

        return errors;
    }

    public static bool SameAs(IReadOnlyDictionary<string, WhatsAppTemplateRef>? left, IReadOnlyDictionary<string, WhatsAppTemplateRef>? right)
    {
        left ??= new Dictionary<string, WhatsAppTemplateRef>();
        right ??= new Dictionary<string, WhatsAppTemplateRef>();
        return left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var other) && other == pair.Value);
    }
}
