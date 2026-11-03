using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class TenantSettingsTests
{
    private static readonly TenantScope Scope = TenantScope.Create("t", "w");

    [Fact]
    public void Defaults_AreTheValuesThatUsedToBeConstants()
    {
        var settings = TenantSettings.Defaults(Scope);

        Assert.True(settings.RequireIraApprovalForPublish);
        Assert.Equal(3, settings.MissingDocumentReminderDays);
        Assert.Equal(24, settings.SlaWarningLeadHours);
        Assert.Equal(50, settings.SeatLimit);
        Assert.Equal(5000, settings.ClaimsMeterLimit);
        Assert.Empty(settings.Validate());
    }

    [Theory]
    [InlineData(0, 24, 50, 5000)]
    [InlineData(3, 169, 50, 5000)]
    [InlineData(3, 24, 0, 5000)]
    [InlineData(3, 24, 50, 1_000_001)]
    public void OutOfRangeValuesAreInvalid(int reminderDays, int leadHours, int seats, int claims)
    {
        var settings = TenantSettings.Defaults(Scope) with
        {
            MissingDocumentReminderDays = reminderDays,
            SlaWarningLeadHours = leadHours,
            SeatLimit = seats,
            ClaimsMeterLimit = claims,
        };

        Assert.Single(settings.Validate());
    }

    [Fact]
    public void ChangedFrom_NamesExactlyWhatDiffers()
    {
        var before = TenantSettings.Defaults(Scope);
        var after = before with { SeatLimit = 10, RequireIraApprovalForPublish = false };

        Assert.Equal(new[] { "RequireIraApprovalForPublish", "SeatLimit" }, after.ChangedFrom(before));
        Assert.Empty(before.ChangedFrom(before));
    }

    [Fact]
    public void PublishingNeedsAStage_Always_AndTheIraReferenceOnlyWhenRequired()
    {
        var withoutIra = Product(stages: DefaultClaimStageLibrary.Default());
        var withoutStages = Product(stages: [], iraReference: "REF", iraDate: new DateOnly(2026, 1, 1));

        Assert.False(withoutIra.CanPublishWith(requireIraApproval: true));
        Assert.True(withoutIra.CanPublishWith(requireIraApproval: false));
        Assert.False(withoutStages.CanPublishWith(requireIraApproval: false));
        Assert.False(withoutIra.CanPublish);
    }

    private static Product Product(IReadOnlyList<ClaimStageDefinition> stages, string? iraReference = null, DateOnly? iraDate = null) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 1,
            Name = "P",
            ProductClass = "General",
            IraApprovalReference = iraReference,
            IraApprovalEffectiveDate = iraDate,
            Status = ProductStatus.Draft,
            Stages = stages,
            DocumentMatrix = [],
            UpdatedByUserId = "u",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "r",
        };
}
