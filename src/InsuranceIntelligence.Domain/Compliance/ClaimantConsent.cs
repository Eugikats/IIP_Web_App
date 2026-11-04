using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Domain.Compliance;

/// <summary>The notice a claimant reads before sending documents: its words and the version those
/// words have, so a consent can say exactly what was agreed to.</summary>
public sealed record PrivacyNotice(int Version, string Text, bool IsDefault)
{
    /// <summary>The key stored on a consent record.</summary>
    public string VersionKey => PrivacyNotices.VersionKey(Version);
}

public static class PrivacyNotices
{
    /// <summary>
    /// Plain-language default shown until an insurer writes its own. A draft: to be confirmed by
    /// legal before an insurer relies on it (the settings page says so). One paragraph per
    /// question a claimant should be able to answer: what, why, who, how long, how to withdraw.
    /// </summary>
    public const string DefaultText =
        "What we collect: the details you give us about your claim, the documents and photos you send, and your contact details."
        + "\n\nWhy: to assess and settle your claim and to keep you updated about it."
        + "\n\nWho sees it: the claims staff who handle your claim, and the service providers who help them process it. We do not sell your information."
        + "\n\nHow long we keep it: while your claim is open and for as long as the law requires after it closes."
        + "\n\nYour choices: you can withdraw your permission at any time from your claim page. We will then stop accepting new documents from you and keep only what the law requires us to keep.";

    public const int MaxLength = 4000;

    public static string VersionKey(int version) => $"v{version}";

    /// <summary>The notice in force for a workspace: its own text when it has written one, else the
    /// default, with the version the workspace has reached.</summary>
    public static PrivacyNotice For(TenantSettings settings) =>
        string.IsNullOrWhiteSpace(settings.PrivacyNoticeText)
            ? new PrivacyNotice(settings.PrivacyNoticeVersion, DefaultText, IsDefault: true)
            : new PrivacyNotice(settings.PrivacyNoticeVersion, settings.PrivacyNoticeText.Trim(), IsDefault: false);
}

public enum ClaimantConsentState
{
    /// <summary>Nothing recorded yet.</summary>
    NotGiven,

    /// <summary>Agreed to the notice that is in force now.</summary>
    Given,

    /// <summary>Agreed once, then withdrawn.</summary>
    Withdrawn,

    /// <summary>Agreed to an earlier wording; the notice has changed since, so it is asked again.</summary>
    NoticeChanged,
}

/// <summary>How a claimant's data-processing consent is identified and judged. The identifier is the
/// verified contact the claimant signed in with, in a normal form, so the same person reaching the
/// portal by email one day and the same email the next is one person.</summary>
public static class ClaimantConsent
{
    public const string Channel = "portal";

    public static string Identifier(string contactValue) =>
        "claimant:" + new string((contactValue ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static ClaimantConsentState StateOf(ConsentRecord? current, PrivacyNotice notice)
    {
        if (current is null)
        {
            return ClaimantConsentState.NotGiven;
        }

        if (current.WithdrawnAt is not null || current.Basis == ConsentBasis.Withdrawn)
        {
            return ClaimantConsentState.Withdrawn;
        }

        return string.Equals(current.NoticeVersion, notice.VersionKey, StringComparison.Ordinal)
            ? ClaimantConsentState.Given
            : ClaimantConsentState.NoticeChanged;
    }
}
