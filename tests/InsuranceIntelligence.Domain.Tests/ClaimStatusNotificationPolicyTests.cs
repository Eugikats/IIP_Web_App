using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 21: the rules for telling a customer their claim's status changed.</summary>
public sealed class ClaimStatusNotificationPolicyTests
{
    [Theory]
    [InlineData(CaseStatus.New, false)]
    [InlineData(CaseStatus.MissingDocuments, false)]
    [InlineData(CaseStatus.Assigned, true)]
    [InlineData(CaseStatus.InReview, true)]
    [InlineData(CaseStatus.ReadyForReview, true)]
    [InlineData(CaseStatus.PendingApproval, true)]
    [InlineData(CaseStatus.ReadyForExport, true)]
    [InlineData(CaseStatus.Escalated, true)]
    [InlineData(CaseStatus.Settled, true)]
    [InlineData(CaseStatus.Closed, true)]
    public void OnlyStatusesWithoutAMessageOfTheirOwnAreSkipped(CaseStatus status, bool sends) =>
        Assert.Equal(sends, ClaimStatusNotificationPolicy.SendsMessage(status));

    [Theory]
    [InlineData(CaseStatus.Assigned, false)]
    [InlineData(CaseStatus.InReview, false)]
    [InlineData(CaseStatus.PendingApproval, false)]
    [InlineData(CaseStatus.ReadyForExport, false)]
    [InlineData(CaseStatus.ReadyForReview, true)]
    [InlineData(CaseStatus.Escalated, true)]
    [InlineData(CaseStatus.Settled, true)]
    [InlineData(CaseStatus.Closed, true)]
    public void MessagesThatAnnounceAnOutcomeOrAnEscalationAlwaysNeedAPerson(CaseStatus status, bool needsApproval) =>
        Assert.Equal(needsApproval, CustomerUpdateApprovalPolicy.RequiresApproval(false, ClaimStatusNotificationPolicy.ReasonFor(status)));

    [Theory]
    [InlineData(true, CaseStatus.InReview, true)]
    [InlineData(false, CaseStatus.InReview, false)]
    [InlineData(true, CaseStatus.Settled, false)]
    [InlineData(true, CaseStatus.Closed, false)]
    [InlineData(true, CaseStatus.Escalated, false)]
    public void AMessageGoesWithoutApprovalOnlyWhenTheWorkspaceAllowsItAndThePolicyDoes(bool autoSend, CaseStatus status, bool mayGo) =>
        Assert.Equal(mayGo, ClaimStatusNotificationPolicy.MaySendWithoutApproval(autoSend, status));

    [Fact]
    public void TheCustomerIsReachedOnTheChannelTheClaimCameInOn()
    {
        Assert.Equal(new StatusMessageRoute(CommunicationChannel.WhatsApp, "+256700000001"), ClaimStatusNotificationPolicy.RouteFor("WhatsApp", " +256700000001 ", "a@b.com"));
        Assert.Equal(new StatusMessageRoute(CommunicationChannel.SMS, "+256700000001"), ClaimStatusNotificationPolicy.RouteFor("Phone", "+256700000001", "a@b.com"));
        Assert.Equal(new StatusMessageRoute(CommunicationChannel.Email, "a@b.com"), ClaimStatusNotificationPolicy.RouteFor("Email", null, " a@b.com "));
        Assert.Null(ClaimStatusNotificationPolicy.RouteFor("Phone", " ", null));
    }

    [Fact]
    public void AWhatsAppClaimWithNoPhoneFallsBackToEmail() =>
        Assert.Equal(new StatusMessageRoute(CommunicationChannel.Email, "a@b.com"), ClaimStatusNotificationPolicy.RouteFor("WhatsApp", "", "a@b.com"));

    [Fact]
    public void TheMessageIsTheWorkspacesWordingWithTheClaimReferenceAndTheFirstName()
    {
        var wording = new StatusWording("We are reviewing your claim", "Our team is going through your documents.");

        var (body, shortBody) = ClaimStatusNotificationPolicy.Compose(wording, "CLM-1A2B3C4D", "Eugene Katusiime");

        Assert.Equal("Hello Eugene. An update on your claim CLM-1A2B3C4D: We are reviewing your claim. Our team is going through your documents.", body);
        Assert.Equal("Claim CLM-1A2B3C4D: We are reviewing your claim.", shortBody);
        Assert.DoesNotContain((char)0x2014, body);
    }

    [Fact]
    public void TheShortFormFitsAnSmsAndANamelessCustomerIsStillGreeted()
    {
        var wording = new StatusWording(new string('a', 60), "Detail.");

        var (body, shortBody) = ClaimStatusNotificationPolicy.Compose(wording, "CLM-1A2B3C4D", "  ");

        Assert.StartsWith("Hello. An update", body);
        Assert.InRange(shortBody.Length, 1, 160);
    }

    [Theory]
    [InlineData("status:InReview", true, CaseStatus.InReview)]
    [InlineData("status:Closed", true, CaseStatus.Closed)]
    [InlineData("status:Nonsense", false, CaseStatus.New)]
    [InlineData("claim-received-portal", false, CaseStatus.New)]
    [InlineData(null, false, CaseStatus.New)]
    public void ASourceKeyTellsWhichStatusAMessageWasAbout(string? key, bool ok, CaseStatus expected)
    {
        Assert.Equal(ok, ClaimStatusNotificationPolicy.TryStatusFromSourceKey(key, out var status));
        if (ok)
        {
            Assert.Equal(expected, status);
            Assert.Equal(key, ClaimStatusNotificationPolicy.SourceKey(status));
        }
    }

    [Theory]
    [InlineData("ApprovalRequired", true)]
    [InlineData("ConfigurationRequired", true)]
    [InlineData("Failed", true)]
    [InlineData("ConsentRequired", true)]
    [InlineData("TemplateRequired", true)]
    [InlineData("Sent", false)]
    [InlineData("SimulatedSent", false)]
    [InlineData(null, false)]
    public void DeliveryStatusesThatMeanItDidNotGoOutAreRecognised(string? status, bool failure) =>
        Assert.Equal(failure, CustomerUpdateDelivery.IsFailure(status));

    [Fact]
    public void EveryFailureHasAPlainReason()
    {
        foreach (var status in new[] { "ApprovalRequired", "ConfigurationRequired", "Failed", "ConsentRequired", "TemplateRequired" })
        {
            Assert.False(string.IsNullOrWhiteSpace(CustomerUpdateDelivery.PlainReason(status)));
        }
    }

    [Fact]
    public void WhatsAppTemplatesAreCheckedTheWayMetaNamesThem()
    {
        var good = new Dictionary<string, WhatsAppTemplateRef> { ["InReview"] = new("claim_in_review", "en"), ["Closed"] = new("claim_closed", "en_GB") };
        Assert.Empty(WhatsAppTemplates.Validate(good));
        Assert.Empty(WhatsAppTemplates.Validate(null));

        Assert.Single(WhatsAppTemplates.Validate(new Dictionary<string, WhatsAppTemplateRef> { ["Nonsense"] = new("a", "en") }));
        Assert.Single(WhatsAppTemplates.Validate(new Dictionary<string, WhatsAppTemplateRef> { ["InReview"] = new("Has Spaces", "en") }));
        Assert.Single(WhatsAppTemplates.Validate(new Dictionary<string, WhatsAppTemplateRef> { ["InReview"] = new("claim_in_review", "english") }));
    }

    [Fact]
    public void SettingsRememberTheChoicesAndSayWhatChanged()
    {
        var scope = TenantScope.Create("t", "w");
        var before = TenantSettings.Defaults(scope);
        Assert.False(before.NotifyCustomerOnStatusChange);
        Assert.False(before.AutoSendStatusMessages);

        var after = before with
        {
            NotifyCustomerOnStatusChange = true,
            AutoSendStatusMessages = true,
            WhatsAppStatusTemplates = new Dictionary<string, WhatsAppTemplateRef> { ["InReview"] = new("claim_in_review", "en") },
        };

        Assert.Equal(
            [nameof(TenantSettings.NotifyCustomerOnStatusChange), nameof(TenantSettings.AutoSendStatusMessages), nameof(TenantSettings.WhatsAppStatusTemplates)],
            after.ChangedFrom(before).ToArray());
        Assert.Empty(after.ChangedFrom(after));
        Assert.Empty(after.Validate());
    }
}
