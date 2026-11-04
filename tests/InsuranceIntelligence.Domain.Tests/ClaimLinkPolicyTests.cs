using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 22: sending a claimant the link to their claim.</summary>
public sealed class ClaimLinkPolicyTests
{
    private static ConsentRecord Consent(bool withdrawn) => new()
    {
        RecordId = Guid.NewGuid(),
        Scope = TenantScope.Create("t", "w"),
        CustomerIdentifier = "x",
        Basis = withdrawn ? ConsentBasis.Withdrawn : ConsentBasis.ExplicitOptIn,
        Purpose = "p",
        Channel = "c",
        WithdrawnAt = withdrawn ? DateTimeOffset.UtcNow : null,
        RecordedByUserId = "u",
        RecordedAt = DateTimeOffset.UtcNow,
        CorrelationId = "c",
    };

    [Fact]
    public void WithoutAChoiceTheClaimsOwnChannelIsUsed()
    {
        var (route, refusal) = ClaimLinkPolicy.Choose(null, "WhatsApp", "+256700000001", "a@b.com");
        Assert.Equal(CommunicationChannel.WhatsApp, route!.Channel);
        Assert.Equal(ClaimLinkRefusal.None, refusal);

        Assert.Equal(CommunicationChannel.SMS, ClaimLinkPolicy.Choose("  ", "Phone", "+256700000001", null).Route!.Channel);
        Assert.Equal(CommunicationChannel.Email, ClaimLinkPolicy.Choose(null, "Email", null, "a@b.com").Route!.Channel);
    }

    [Fact]
    public void ABadOrUnavailableChoiceIsRefusedWithAReason()
    {
        Assert.Equal(ClaimLinkRefusal.NoContact, ClaimLinkPolicy.Choose(null, "Phone", null, null).Refusal);
        Assert.Equal(ClaimLinkRefusal.ChannelHasNoContact, ClaimLinkPolicy.Choose("Email", "Phone", "+256700000001", null).Refusal);
        Assert.Equal(ClaimLinkRefusal.ChannelHasNoContact, ClaimLinkPolicy.Choose("SMS", "Email", null, "a@b.com").Refusal);
        Assert.Equal(ClaimLinkRefusal.ChannelHasNoContact, ClaimLinkPolicy.Choose("Carrier pigeon", "Phone", "+256700000001", "a@b.com").Refusal);
        Assert.Null(ClaimLinkPolicy.Choose("Email", "Phone", "+256700000001", null).Route);
    }

    [Fact]
    public void StaffCanPickAnyChannelTheClaimHasAContactFor()
    {
        Assert.Equal(CommunicationChannel.Email, ClaimLinkPolicy.Choose("email", "Phone", "+256700000001", "a@b.com").Route!.Channel);
        Assert.Equal(CommunicationChannel.WhatsApp, ClaimLinkPolicy.Choose("WhatsApp", "Phone", "+256700000001", null).Route!.Channel);
    }

    [Fact]
    public void WhatsAppNeedsAnUnwithdrawnConsentWhileTextAndEmailOnlyStopAfterAWithdrawal()
    {
        Assert.Equal(ClaimLinkRefusal.ConsentRequired, ClaimLinkPolicy.ConsentCheck(CommunicationChannel.WhatsApp, null));
        Assert.Equal(ClaimLinkRefusal.ConsentRequired, ClaimLinkPolicy.ConsentCheck(CommunicationChannel.WhatsApp, Consent(withdrawn: true)));
        Assert.Equal(ClaimLinkRefusal.None, ClaimLinkPolicy.ConsentCheck(CommunicationChannel.WhatsApp, Consent(withdrawn: false)));

        foreach (var channel in new[] { CommunicationChannel.SMS, CommunicationChannel.Email })
        {
            Assert.Equal(ClaimLinkRefusal.None, ClaimLinkPolicy.ConsentCheck(channel, null));
            Assert.Equal(ClaimLinkRefusal.None, ClaimLinkPolicy.ConsentCheck(channel, Consent(withdrawn: false)));
            Assert.Equal(ClaimLinkRefusal.ConsentWithdrawn, ClaimLinkPolicy.ConsentCheck(channel, Consent(withdrawn: true)));
        }
    }

    [Fact]
    public void EachChannelUsesItsOwnConsentPurpose()
    {
        Assert.Equal(ConsentPurposes.WhatsApp, ClaimLinkPolicy.PurposeFor(CommunicationChannel.WhatsApp));
        Assert.Equal(ConsentPurposes.Sms, ClaimLinkPolicy.PurposeFor(CommunicationChannel.SMS));
        Assert.Equal(ConsentPurposes.Email, ClaimLinkPolicy.PurposeFor(CommunicationChannel.Email));
    }

    [Fact]
    public void TheMessageCarriesTheLinkAndSaysItWorksOnceForADay()
    {
        var (body, shortBody) = ClaimLinkPolicy.Compose("Eugene Katusiime", "CLM-1A2B3C4D", "https://portal.example/access/verify?token=abc");

        Assert.StartsWith("Hello Eugene.", body);
        Assert.Contains("CLM-1A2B3C4D", body);
        Assert.Contains("works once and for 24 hours", body);
        Assert.EndsWith("token=abc", body);
        Assert.Contains("token=abc", shortBody);
        Assert.DoesNotContain((char)0x2014, body);
        Assert.StartsWith("Hello.", ClaimLinkPolicy.Compose("  ", "CLM-1", "l").Body);
    }

    [Theory]
    [InlineData("+256700123456", "+256*******56")]
    [InlineData("a.person@example.com", "a***@example.com")]
    [InlineData("12345", "*****")]
    public void TheAddressIsShownWithItsMiddleHidden(string recipient, string masked) =>
        Assert.Equal(masked, ClaimLinkPolicy.Mask(recipient));

    [Fact]
    public void EveryRefusalHasAPlainReasonAndTheLinkLivesForADay()
    {
        foreach (var refusal in new[] { ClaimLinkRefusal.NoContact, ClaimLinkRefusal.ChannelHasNoContact, ClaimLinkRefusal.ConsentWithdrawn, ClaimLinkRefusal.ConsentRequired })
        {
            Assert.False(string.IsNullOrWhiteSpace(ClaimLinkPolicy.PlainReason(refusal)));
        }

        Assert.Equal(TimeSpan.FromHours(24), ClaimLinkPolicy.LinkLifetime);
    }
}
