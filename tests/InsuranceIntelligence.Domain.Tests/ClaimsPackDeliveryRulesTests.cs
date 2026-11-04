using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 25: who a claims pack may be emailed to, when it goes, and how it travels.</summary>
public sealed class ClaimsPackDeliveryRulesTests
{
    [Fact]
    public void RecipientsAreTrimmedAndDeduplicatedWithoutRegardToCase()
    {
        var list = ClaimsPackDeliveryRules.Normalise([" Re@Example.com ", "re@example.com", "", "  ", "other@example.com"]);

        Assert.Equal(["Re@Example.com", "other@example.com"], list);
        Assert.Empty(ClaimsPackDeliveryRules.Normalise(null));
    }

    [Theory]
    [InlineData("good@example.com", true)]
    [InlineData("Name <good@example.com>", false)]
    [InlineData("not-an-address", false)]
    [InlineData("a@b", false)]
    [InlineData("two@example.com, three@example.com", false)]
    public void OnlyPlainValidAddressesAreAccepted(string address, bool valid) =>
        Assert.Equal(valid, ClaimsPackDeliveryRules.Validate([address], "Manual", null).Count == 0);

    [Fact]
    public void NoMoreThanTenAddressesAreAllowed()
    {
        var many = Enumerable.Range(0, 11).Select(index => $"person{index}@example.com").ToArray();

        Assert.Single(ClaimsPackDeliveryRules.Validate(many, "Manual", null));
        Assert.Empty(ClaimsPackDeliveryRules.Validate(many.Take(10).ToArray(), "Manual", null));
    }

    [Fact]
    public void AnAutomaticTriggerNeedsSomeoneToSendTo()
    {
        Assert.Single(ClaimsPackDeliveryRules.Validate([], "Complete", null));
        Assert.Single(ClaimsPackDeliveryRules.Validate(null, "Status", "Closed"));
        Assert.Empty(ClaimsPackDeliveryRules.Validate([], "Manual", null));
        Assert.Empty(ClaimsPackDeliveryRules.Validate(["a@example.com"], "Complete", null));
    }

    [Fact]
    public void TheStatusTriggerNeedsARealStatusAndAnUnknownTriggerIsRefused()
    {
        Assert.Single(ClaimsPackDeliveryRules.Validate(["a@example.com"], "Status", null));
        Assert.Single(ClaimsPackDeliveryRules.Validate(["a@example.com"], "Status", "Nonsense"));
        Assert.Empty(ClaimsPackDeliveryRules.Validate(["a@example.com"], "Status", "Closed"));
        Assert.Single(ClaimsPackDeliveryRules.Validate(["a@example.com"], "Whenever", null));
    }

    [Theory]
    [InlineData(ClaimsPackTrigger.Complete, 100, true)]
    [InlineData(ClaimsPackTrigger.Complete, 99.9, false)]
    [InlineData(ClaimsPackTrigger.Manual, 100, false)]
    [InlineData(ClaimsPackTrigger.Status, 100, false)]
    public void CompletenessSendsThePackOnlyForTheCompleteTriggerAndOnlyAtOneHundred(ClaimsPackTrigger trigger, double score, bool sends) =>
        Assert.Equal(sends, ClaimsPackDeliveryRules.CompletenessTriggers(trigger, (decimal)score));

    [Fact]
    public void AStatusChangeSendsThePackOnlyWhenItReachesTheChosenStatus()
    {
        Assert.True(ClaimsPackDeliveryRules.StatusTriggers(ClaimsPackTrigger.Status, "Closed", CaseStatus.Escalated, CaseStatus.Closed));
        Assert.True(ClaimsPackDeliveryRules.StatusTriggers(ClaimsPackTrigger.Status, "closed", CaseStatus.Escalated, CaseStatus.Closed));
        Assert.False(ClaimsPackDeliveryRules.StatusTriggers(ClaimsPackTrigger.Status, "Closed", CaseStatus.New, CaseStatus.InReview));
        Assert.False(ClaimsPackDeliveryRules.StatusTriggers(ClaimsPackTrigger.Status, "Closed", CaseStatus.Closed, CaseStatus.Closed));
        Assert.False(ClaimsPackDeliveryRules.StatusTriggers(ClaimsPackTrigger.Complete, "Closed", CaseStatus.Escalated, CaseStatus.Closed));
        Assert.False(ClaimsPackDeliveryRules.StatusTriggers(ClaimsPackTrigger.Status, null, CaseStatus.Escalated, CaseStatus.Closed));
    }

    [Fact]
    public void AddressesAreShownWithTheirMiddleHidden()
    {
        Assert.Equal("a***@example.com", ClaimsPackDeliveryRules.MaskAddress("assessor@example.com"));
        Assert.Equal("x@y.com", ClaimsPackDeliveryRules.MaskAddress("x@y.com"));
    }

    [Fact]
    public void ALinkWorksForAWeekAndAFewDownloadsThenStops()
    {
        var now = DateTimeOffset.UtcNow;
        var link = new ClaimsPackShareLink
        {
            LinkId = Guid.NewGuid(),
            Scope = TenantScope.Create("t", "w"),
            CaseId = Guid.NewGuid(),
            TokenHash = "h",
            ObjectKey = "k",
            FileName = "f.zip",
            SizeBytes = 10,
            CreatedAt = now,
            ExpiresAt = now.Add(ClaimsPackDeliveryRules.LinkLifetime),
        };

        Assert.True(link.IsUsableAt(now.AddDays(6)));
        Assert.False(link.IsUsableAt(now.AddDays(8)));
        Assert.True((link with { DownloadCount = ClaimsPackDeliveryRules.MaxLinkDownloads - 1 }).IsUsableAt(now));
        Assert.False((link with { DownloadCount = ClaimsPackDeliveryRules.MaxLinkDownloads }).IsUsableAt(now));
        Assert.Equal(TimeSpan.FromDays(7), ClaimsPackDeliveryRules.LinkLifetime);
    }

    [Fact]
    public void PacksUpToThreeMegabytesAreAttachedAndLargerOnesAreLinked() =>
        Assert.Equal(3L * 1024 * 1024, ClaimsPackDeliveryRules.MaxAttachmentBytes);
}
