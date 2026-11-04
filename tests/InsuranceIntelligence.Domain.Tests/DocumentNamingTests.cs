using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class DocumentNamingTests
{
    [Theory]
    [InlineData("Police report", "PoliceReport")]
    [InlineData("  vehicle   log book ", "VehicleLogBook")]
    [InlineData("Customer ID (front)", "CustomerIDFront")]
    [InlineData("Claim-form", "Claimform")]
    [InlineData("", "Unsorted")]
    [InlineData("---", "Unsorted")]
    public void ALabelBecomesOneWordWithNoSpacesOrPunctuation(string label, string expected)
    {
        Assert.Equal(expected, DocumentNaming.Compact(label));
    }

    [Fact]
    public void TheNameIsLabelClaimReferenceNumberAndOriginalExtension()
    {
        Assert.Equal("PoliceReport_CLM-1A2B3C4D.jpg", DocumentNaming.DisplayName("Police report", "CLM-1A2B3C4D", 1, "IMG_0042.JPG"));
        Assert.Equal("PoliceReport_CLM-1A2B3C4D_2.jpg", DocumentNaming.DisplayName("Police report", "CLM-1A2B3C4D", 2, "IMG_0042.JPG"));
        Assert.Equal("Unsorted_CLM-1A2B3C4D", DocumentNaming.DisplayName("", "CLM-1A2B3C4D", 1, "no_extension"));
    }

    [Fact]
    public void ARecognitionIsConfidentOnlyWithATypeAndEnoughCertainty()
    {
        DocumentRecognition Make(string? key, decimal confidence) => new(key, confidence, "r", "p", null, DateTimeOffset.UtcNow);

        Assert.True(Make("PoliceAbstract", 0.8m).IsConfident);
        Assert.False(Make("PoliceAbstract", 0.79m).IsConfident);
        Assert.False(Make(null, 0.99m).IsConfident);
    }

    [Fact]
    public void TheCatalogueIsTheBuiltInTypesWithTheInsurersOwnWordsAndAdditionsAndWithoutRetiredOnes()
    {
        DocumentTypeDefinition Define(string key, string label, bool active) => new()
        {
            DefinitionId = Guid.NewGuid(), VersionId = Guid.NewGuid(), Scope = TenantScope.Create("t", "w"), Version = 1, Key = key,
            CustomerLabel = label, Description = "d", Active = active, UpdatedByUserId = "u", UpdatedAt = DateTimeOffset.UtcNow, ChangeReason = "r",
        };

        var catalogue = DocumentCatalogue.Build([
            Define("PoliceAbstract", "Report from the police", true),
            Define("LogBook", "Vehicle log book", true),
            Define("Invoice", "Invoice", false),
        ]);

        Assert.Contains(catalogue, entry => entry.Key == "PoliceAbstract" && entry.Label == "Report from the police");
        Assert.Contains(catalogue, entry => entry.Key == "LogBook");
        Assert.DoesNotContain(catalogue, entry => entry.Key == "Invoice");
        Assert.DoesNotContain(catalogue, entry => entry.Key == "Unknown");
        Assert.Contains(catalogue, entry => entry.Key == "ClaimForm");
    }

    [Fact]
    public void AClaimsReferenceIsTheSameOneUsedEverywhereElse()
    {
        var id = Guid.Parse("1a2b3c4d-0000-0000-0000-000000000000");

        Assert.Equal("CLM-1A2B3C4D", ClaimReference.For(id));
    }
}
