using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class ClaimantConsentTests
{
    private static readonly TenantScope Scope = TenantScope.Create("t", "w");

    private static ConsentRecord Record(ConsentBasis basis, string? noticeVersion, DateTimeOffset? withdrawnAt = null) =>
        new()
        {
            RecordId = Guid.NewGuid(), Scope = Scope, CustomerIdentifier = "claimant:a", Basis = basis,
            Purpose = ConsentPurposes.ClaimDataProcessing, Channel = "portal", WithdrawnAt = withdrawnAt,
            RecordedByUserId = "claimant:a", RecordedAt = DateTimeOffset.UtcNow, CorrelationId = "c", NoticeVersion = noticeVersion,
        };

    [Fact]
    public void TheIdentifierIsTheSameForTheSameContactWhateverItsFormatting()
    {
        Assert.Equal(ClaimantConsent.Identifier("Sarah.K@Example.com"), ClaimantConsent.Identifier(" sarah.k@example.COM "));
        Assert.Equal(ClaimantConsent.Identifier("+256 700 000 123"), ClaimantConsent.Identifier("256700000123"));
        Assert.NotEqual(ClaimantConsent.Identifier("a@example.com"), ClaimantConsent.Identifier("b@example.com"));
    }

    [Fact]
    public void ConsentIsGivenOnlyForTheNoticeInForce()
    {
        var notice = new PrivacyNotice(2, "text", IsDefault: false);

        Assert.Equal(ClaimantConsentState.NotGiven, ClaimantConsent.StateOf(null, notice));
        Assert.Equal(ClaimantConsentState.Given, ClaimantConsent.StateOf(Record(ConsentBasis.ExplicitOptIn, "v2"), notice));
        Assert.Equal(ClaimantConsentState.NoticeChanged, ClaimantConsent.StateOf(Record(ConsentBasis.ExplicitOptIn, "v1"), notice));
        Assert.Equal(ClaimantConsentState.NoticeChanged, ClaimantConsent.StateOf(Record(ConsentBasis.ExplicitOptIn, null), notice));
        Assert.Equal(ClaimantConsentState.Withdrawn, ClaimantConsent.StateOf(Record(ConsentBasis.Withdrawn, "v2", DateTimeOffset.UtcNow), notice));
    }

    [Fact]
    public void TheNoticeIsTheWorkspacesOwnWhenWrittenElseTheDefault()
    {
        var defaults = PrivacyNotices.For(TenantSettings.Defaults(Scope));
        var custom = PrivacyNotices.For(TenantSettings.Defaults(Scope) with { PrivacyNoticeText = "  Our words  ", PrivacyNoticeVersion = 4 });

        Assert.True(defaults.IsDefault);
        Assert.Equal(PrivacyNotices.DefaultText, defaults.Text);
        Assert.Equal("v1", defaults.VersionKey);
        Assert.False(custom.IsDefault);
        Assert.Equal("Our words", custom.Text);
        Assert.Equal("v4", custom.VersionKey);
    }

    [Fact]
    public void TheDefaultNoticeAnswersTheFiveQuestionsAndUsesNoEmDash()
    {
        foreach (var part in new[] { "What we collect:", "Why:", "Who sees it:", "How long we keep it:", "Your choices:" })
        {
            Assert.Contains(part, PrivacyNotices.DefaultText);
        }

        Assert.DoesNotContain((char)0x2014, PrivacyNotices.DefaultText);
        Assert.True(PrivacyNotices.DefaultText.Length < PrivacyNotices.MaxLength);
    }

    [Fact]
    public void ATooLongNoticeIsInvalid()
    {
        var settings = TenantSettings.Defaults(Scope) with { PrivacyNoticeText = new string('a', PrivacyNotices.MaxLength + 1) };

        Assert.Single(settings.Validate());
        Assert.Contains(nameof(TenantSettings.PrivacyNoticeText), settings.ChangedFrom(TenantSettings.Defaults(Scope)));
    }
}
