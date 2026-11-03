using System.Text.Json;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>Build-loop item 4: required documents per claim type, with a kind and limits.</summary>
public sealed class DocumentRequirementTests
{
    private const string Stage = "acknowledgement_and_document_request";

    private static ProductDocumentRequirement Entry(string key, string label, bool required, params string[] claimTypes) =>
        new(key, label, "Because.", required, Stage, false, claimTypes);

    private static Product ProductWith(params ProductDocumentRequirement[] matrix) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = TenantScope.Create("t", "w"),
            Version = 1,
            Name = "Motor Cover",
            ProductClass = "Motor",
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = matrix,
            UpdatedByUserId = "u",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "test",
        };

    [Fact]
    public void AnEntryWithNoClaimTypes_AppliesToEveryClaimType_EvenAClaimWithNone()
    {
        var entry = Entry("ClaimForm", "Claim form", true);

        Assert.True(entry.AppliesTo("theft"));
        Assert.True(entry.AppliesTo(null));
    }

    [Fact]
    public void AnEntryForSpecificClaimTypes_AppliesOnlyToThose()
    {
        var entry = Entry("PoliceAbstract", "Police report", true, "theft", "own-damage");

        Assert.True(entry.AppliesTo("THEFT"));
        Assert.False(entry.AppliesTo("windscreen"));
        Assert.False(entry.AppliesTo(null));
    }

    [Fact]
    public void ForProduct_ReturnsOnlyTheDocumentsThatApplyToTheClaimType()
    {
        var product = ProductWith(
            Entry("ClaimForm", "Claim form", true),
            Entry("PoliceAbstract", "Police report", true, "theft"),
            Entry("GarageEstimate", "Garage estimate", false, "own-damage"));

        var theft = DocumentChecklistRules.ForProduct(product, "theft")!;
        var ownDamage = DocumentChecklistRules.ForProduct(product, "own-damage")!;
        var noType = DocumentChecklistRules.ForProduct(product, null)!;

        Assert.Equal(new[] { "ClaimForm", "PoliceAbstract" }, theft.Slots.Select(slot => slot.Key));
        Assert.Equal(new[] { "ClaimForm", "GarageEstimate" }, ownDamage.Slots.Select(slot => slot.Key));
        Assert.Equal(new[] { "ClaimForm" }, noType.Slots.Select(slot => slot.Key));
    }

    [Fact]
    public void ForProduct_RequiredSlotsComeBeforeOptionalOnes()
    {
        var product = ProductWith(
            Entry("GarageEstimate", "Garage estimate", false),
            Entry("ClaimForm", "Claim form", true));

        var slots = DocumentChecklistRules.ForProduct(product, null)!.Slots;

        Assert.Equal(new[] { true, false }, slots.Select(slot => slot.Required));
    }

    [Fact]
    public void ForProduct_ANewDocumentSpecificToTheTypeOverridesTheGeneralOneForTheSameKey()
    {
        var product = ProductWith(
            Entry("PoliceAbstract", "Police report", false),
            Entry("PoliceAbstract", "Police report (within 24 hours)", true, "theft"));

        var theft = DocumentChecklistRules.ForProduct(product, "theft")!.Slots.Single();
        var other = DocumentChecklistRules.ForProduct(product, "own-damage")!.Slots.Single();

        Assert.True(theft.Required);
        Assert.Equal("Police report (within 24 hours)", theft.Label);
        Assert.False(other.Required);
    }

    [Fact]
    public void ForProduct_ReturnsAnEmptySet_WhenTheMatrixHasNothingForThisClaimType()
    {
        var product = ProductWith(Entry("PoliceAbstract", "Police report", true, "theft"));

        var rules = DocumentChecklistRules.ForProduct(product, "windscreen");

        Assert.NotNull(rules);
        Assert.Empty(rules!.Slots);
        Assert.Empty(rules.RequiredDocuments);
    }

    [Fact]
    public void ForProduct_ReturnsNull_ForAProductWithNoMatrixSoTheBuiltInRulesApply() =>
        Assert.Null(DocumentChecklistRules.ForProduct(ProductWith(), "theft"));

    [Fact]
    public void Defaults_PhotosOfTheDamageAreEvidenceWithSeveralImages()
    {
        var slot = Entry("PhotoEvidence", "Photos", true).ToSlot();

        Assert.Equal(DocumentKind.Evidence, slot.Kind);
        Assert.True(slot.AllowsMultipleFiles);
        Assert.Equal(DocumentSlotDefaults.DefaultEvidenceLimit, slot.MaxFiles);
        Assert.Equal(DocumentSlotDefaults.EvidenceFormats, slot.AcceptedFormats);
        Assert.False(slot.Accepts("scan.pdf"));
        Assert.True(slot.Accepts("IMG_1.JPG"));
    }

    [Fact]
    public void Defaults_PaperworkIsOneFile_UntilPagesAreAllowed()
    {
        var single = Entry("ClaimForm", "Form", true).ToSlot();
        var pages = (Entry("ClaimForm", "Form", true) with { AllowsMultiplePages = true }).ToSlot();

        Assert.Equal(DocumentKind.Document, single.Kind);
        Assert.False(single.AllowsMultipleFiles);
        Assert.Equal(1, single.MaxFiles);
        Assert.True(pages.AllowsMultipleFiles);
        Assert.Equal(DocumentSlotDefaults.DefaultMultiPageLimit, pages.MaxFiles);
    }

    [Fact]
    public void Configured_LimitsWinOverDefaults_ButASingleFileSlotIsNeverMoreThanOne()
    {
        var tuned = (Entry("PhotoEvidence", "Photos", true) with { MaxFiles = 4, AcceptedFormats = ["jpg"] }).ToSlot();
        var singleWithLimit = (Entry("ClaimForm", "Form", true) with { MaxFiles = 5 }).ToSlot();

        Assert.Equal(4, tuned.MaxFiles);
        Assert.Equal(new[] { "jpg" }, tuned.AcceptedFormats);
        Assert.Equal(1, singleWithLimit.MaxFiles);
    }

    [Fact]
    public void ARequirementStoredBeforeKindsExisted_StillReadsAsItAlwaysDid()
    {
        const string oldJson = "{\"DocumentKey\":\"PoliceAbstract\",\"CustomerLabel\":\"Police report\",\"Reason\":\"r\",\"IsRequired\":true,\"RequiredAtStageKey\":\"s\",\"IsRegulatoryOrWordingMandated\":false}";

        var entry = JsonSerializer.Deserialize<ProductDocumentRequirement>(oldJson)!;

        Assert.True(entry.AppliesTo("anything"));
        var slot = entry.ToSlot();
        Assert.Equal(DocumentKind.Document, slot.Kind);
        Assert.Equal(1, slot.MaxFiles);
    }

    [Fact]
    public void BuiltInRules_AlsoProduceSlots_WithPhotosAsEvidence()
    {
        var slots = DocumentChecklistRules.ForProductLine("Motor").Slots;

        Assert.Equal(new[] { "ClaimForm", "PoliceAbstract", "PhotoEvidence", "Invoice", "GarageEstimate", "CustomerId" }, slots.Select(slot => slot.Key));
        Assert.Equal(DocumentKind.Evidence, slots.Single(slot => slot.Key == "PhotoEvidence").Kind);
        Assert.Equal("Police abstract", slots.Single(slot => slot.Key == "PoliceAbstract").Label);
    }
}
