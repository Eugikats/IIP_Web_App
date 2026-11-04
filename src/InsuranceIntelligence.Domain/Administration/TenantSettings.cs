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
        return changed;
    }
}

public interface ITenantSettingsRepository
{
    /// <summary>The workspace's settings, or the defaults when nothing has been saved.</summary>
    Task<TenantSettings> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(TenantSettings settings, CancellationToken cancellationToken = default);
}
