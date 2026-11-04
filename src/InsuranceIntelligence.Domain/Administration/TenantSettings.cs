using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Administration;

/// <summary>
/// The operating values an insurer can tune without a developer and without a release
/// (build-loop item 6). Each used to be a constant in code; the defaults below are exactly those
/// old values, so a workspace that never changes anything behaves as it always did.
/// </summary>
public sealed record TenantSettings
{
    public const int DefaultMissingDocumentReminderDays = 3;
    public const int DefaultSlaWarningLeadHours = 24;
    public const int DefaultSeatLimit = 50;
    public const int DefaultClaimsMeterLimit = 5000;

    public required TenantScope Scope { get; init; }

    /// <summary>When true (the default, and the rule for an insurer regulated by the IRA), a product
    /// cannot be published without an approval reference and its effective date. An insurer with no
    /// such regulator can switch it off. A second person still has to approve every publish.</summary>
    public bool RequireIraApprovalForPublish { get; init; } = true;

    /// <summary>How many days a required document can be missing before the customer is reminded,
    /// and again after the same wait.</summary>
    public int MissingDocumentReminderDays { get; init; } = DefaultMissingDocumentReminderDays;

    /// <summary>How many hours before a claim's SLA is breached the officer is warned.</summary>
    public int SlaWarningLeadHours { get; init; } = DefaultSlaWarningLeadHours;

    /// <summary>The most staff accounts the workspace may have.</summary>
    public int SeatLimit { get; init; } = DefaultSeatLimit;

    /// <summary>The number of claims the usage meter measures against.</summary>
    public int ClaimsMeterLimit { get; init; } = DefaultClaimsMeterLimit;

    /// <summary>The privacy notice a claimant reads before sending documents. Null means the default
    /// text (see PrivacyNotices). The server raises the version each time the text changes, so a
    /// consent can name the exact wording it agreed to.</summary>
    public string? PrivacyNoticeText { get; init; }

    public int PrivacyNoticeVersion { get; init; } = 1;

    /// <summary>The workspace's own words for what a customer reads about their claim's status, keyed
    /// by status name (build-loop item 19). A status with no entry uses the default wording.</summary>
    public IReadOnlyDictionary<string, Cases.StatusWording>? ClaimantStatusWording { get; init; }

    /// <summary>Whether the customer is told, with the workspace's own status wording, when their claim's
    /// status changes (build-loop item 21). Off until an administrator turns it on.</summary>
    public bool NotifyCustomerOnStatusChange { get; init; }

    /// <summary>Whether a status message may go out without a person approving it, for the kinds of message
    /// the customer update policy allows that for. Statuses that announce an outcome always need a person.</summary>
    public bool AutoSendStatusMessages { get; init; }

    /// <summary>The WhatsApp template Meta approved for each status, by status name. WhatsApp only allows a
    /// template once the customer's last message is more than 24 hours old.</summary>
    public IReadOnlyDictionary<string, Cases.WhatsAppTemplateRef>? WhatsAppStatusTemplates { get; init; }

    /// <summary>Who a claims pack is emailed to (build-loop item 25), such as a reinsurer, an assessor or the insurer's own
    /// claims inbox. Empty means nobody, so nothing is sent automatically.</summary>
    public IReadOnlyList<string>? ClaimsPackRecipients { get; init; }

    /// <summary>When the pack is emailed by itself: Manual (never), Complete or Status.</summary>
    public string ClaimsPackTrigger { get; init; } = "Manual";

    /// <summary>For the Status trigger: the status that sends it.</summary>
    public string? ClaimsPackTriggerStatus { get; init; }

    /// <summary>Whether a returning customer is offered the details and documents they gave on an earlier claim (build-loop item 26).</summary>
    public bool CustomerMemoryEnabled { get; init; } = true;

    /// <summary>The detail keys worth carrying between a customer's claims. Empty means the platform's short default list.</summary>
    public IReadOnlyList<string>? CustomerMemoryFieldKeys { get; init; }

    /// <summary>The document kinds a customer may reuse from an earlier claim. Empty means proof of identity only.</summary>
    public IReadOnlyList<string>? CustomerMemoryDocumentKeys { get; init; }

    /// <summary>Whose assistant Kato is for this workspace (build-loop item 27): the organisation's name. Empty means the
    /// deployment's default brand.</summary>
    public string? AssistantOrganisation { get; init; }

    /// <summary>The short name Kato uses in conversation. Empty means the organisation's name.</summary>
    public string? AssistantShortName { get; init; }

    /// <summary>For a bank or agent selling other insurers' covers: those insurers, comma separated. Empty means the
    /// organisation sells its own covers only.</summary>
    public string? AssistantPartnerInsurers { get; init; }

    public string? UpdatedByUserId { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public static TenantSettings Defaults(TenantScope scope) => new() { Scope = scope };

    /// <summary>Why these settings cannot be saved, in words an administrator can act on.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (MissingDocumentReminderDays is < 1 or > 30)
        {
            errors.Add("The reminder wait must be between 1 and 30 days.");
        }

        if (SlaWarningLeadHours is < 1 or > 168)
        {
            errors.Add("The SLA warning lead time must be between 1 and 168 hours.");
        }

        if (SeatLimit is < 1 or > 10_000)
        {
            errors.Add("The seat limit must be between 1 and 10,000.");
        }

        if (ClaimsMeterLimit is < 1 or > 1_000_000)
        {
            errors.Add("The claims allowance must be between 1 and 1,000,000.");
        }

        if (PrivacyNoticeText is { } notice && notice.Length > Compliance.PrivacyNotices.MaxLength)
        {
            errors.Add($"The privacy notice can be at most {Compliance.PrivacyNotices.MaxLength:N0} characters.");
        }

        errors.AddRange(Cases.ClaimantStatusWords.Validate(ClaimantStatusWording));
        errors.AddRange(Cases.WhatsAppTemplates.Validate(WhatsAppStatusTemplates));
        errors.AddRange(Compliance.ClaimsPackDeliveryRules.Validate(ClaimsPackRecipients, ClaimsPackTrigger, ClaimsPackTriggerStatus));
        foreach (var key in (CustomerMemoryFieldKeys ?? []).Concat(CustomerMemoryDocumentKeys ?? []))
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 60 || !key.All(character => char.IsLetterOrDigit(character) || character is '_' or '-'))
            {
                errors.Add($"'{key}' is not a valid detail or document key (letters, digits, dashes and underscores only).");
            }
        }

        if ((CustomerMemoryFieldKeys?.Count ?? 0) > 30 || (CustomerMemoryDocumentKeys?.Count ?? 0) > 30)
        {
            errors.Add("At most 30 keys can be listed for customer memory.");
        }

        if ((AssistantOrganisation?.Length ?? 0) > 120 || (AssistantShortName?.Length ?? 0) > 60 || (AssistantPartnerInsurers?.Length ?? 0) > 600)
        {
            errors.Add("The assistant's organisation can be at most 120 characters, its short name 60, and the partner insurers 600.");
        }

        if (!string.IsNullOrWhiteSpace(AssistantPartnerInsurers) && string.IsNullOrWhiteSpace(AssistantOrganisation))
        {
            errors.Add("Name the organisation the assistant speaks for before listing partner insurers.");
        }

        return errors;
    }

    /// <summary>The names of the settings that differ between two values, for the audit trail.</summary>
    public IReadOnlyList<string> ChangedFrom(TenantSettings previous)
    {
        var changed = new List<string>();
        if (RequireIraApprovalForPublish != previous.RequireIraApprovalForPublish) changed.Add(nameof(RequireIraApprovalForPublish));
        if (MissingDocumentReminderDays != previous.MissingDocumentReminderDays) changed.Add(nameof(MissingDocumentReminderDays));
        if (SlaWarningLeadHours != previous.SlaWarningLeadHours) changed.Add(nameof(SlaWarningLeadHours));
        if (SeatLimit != previous.SeatLimit) changed.Add(nameof(SeatLimit));
        if (ClaimsMeterLimit != previous.ClaimsMeterLimit) changed.Add(nameof(ClaimsMeterLimit));
        if (PrivacyNoticeText != previous.PrivacyNoticeText) changed.Add(nameof(PrivacyNoticeText));
        if (!Cases.ClaimantStatusWords.SameAs(ClaimantStatusWording, previous.ClaimantStatusWording)) changed.Add(nameof(ClaimantStatusWording));
        if (NotifyCustomerOnStatusChange != previous.NotifyCustomerOnStatusChange) changed.Add(nameof(NotifyCustomerOnStatusChange));
        if (AutoSendStatusMessages != previous.AutoSendStatusMessages) changed.Add(nameof(AutoSendStatusMessages));
        if (!Cases.WhatsAppTemplates.SameAs(WhatsAppStatusTemplates, previous.WhatsAppStatusTemplates)) changed.Add(nameof(WhatsAppStatusTemplates));
        if (!(ClaimsPackRecipients ?? []).SequenceEqual(previous.ClaimsPackRecipients ?? [], StringComparer.OrdinalIgnoreCase)) changed.Add(nameof(ClaimsPackRecipients));
        if (!string.Equals(ClaimsPackTrigger, previous.ClaimsPackTrigger, StringComparison.Ordinal)) changed.Add(nameof(ClaimsPackTrigger));
        if (!string.Equals(ClaimsPackTriggerStatus, previous.ClaimsPackTriggerStatus, StringComparison.Ordinal)) changed.Add(nameof(ClaimsPackTriggerStatus));
        if (!string.Equals(AssistantOrganisation, previous.AssistantOrganisation, StringComparison.Ordinal)) changed.Add(nameof(AssistantOrganisation));
        if (!string.Equals(AssistantShortName, previous.AssistantShortName, StringComparison.Ordinal)) changed.Add(nameof(AssistantShortName));
        if (!string.Equals(AssistantPartnerInsurers, previous.AssistantPartnerInsurers, StringComparison.Ordinal)) changed.Add(nameof(AssistantPartnerInsurers));
        if (CustomerMemoryEnabled != previous.CustomerMemoryEnabled) changed.Add(nameof(CustomerMemoryEnabled));
        if (!(CustomerMemoryFieldKeys ?? []).SequenceEqual(previous.CustomerMemoryFieldKeys ?? [], StringComparer.Ordinal)) changed.Add(nameof(CustomerMemoryFieldKeys));
        if (!(CustomerMemoryDocumentKeys ?? []).SequenceEqual(previous.CustomerMemoryDocumentKeys ?? [], StringComparer.Ordinal)) changed.Add(nameof(CustomerMemoryDocumentKeys));
        return changed;
    }
}

public interface ITenantSettingsRepository
{
    /// <summary>The workspace's settings, or the defaults when nothing has been saved.</summary>
    Task<TenantSettings> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(TenantSettings settings, CancellationToken cancellationToken = default);
}
