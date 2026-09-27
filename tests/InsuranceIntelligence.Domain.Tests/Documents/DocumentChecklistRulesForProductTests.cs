using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Tests.Documents;

public sealed class DocumentChecklistRulesForProductTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");

    [Fact]
    public void ForProduct_MapsDocumentKeysToDocumentTypesByName()
    {
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("PoliceAbstract", "Police report", "Required by regulator", IsRequired: true, "intimation_and_reference", true),
            new ProductDocumentRequirement("GarageEstimate", "Repair estimate", "Assessing damage cost", IsRequired: false, "intimation_and_reference", false),
        ]);

        var rules = DocumentChecklistRules.ForProduct(product);

        Assert.NotNull(rules);
        Assert.Contains(DocumentType.PoliceAbstract, rules!.RequiredDocuments);
        Assert.Contains(DocumentType.GarageEstimate, rules.OptionalDocuments);
        Assert.DoesNotContain(DocumentType.GarageEstimate, rules.RequiredDocuments);
    }

    [Fact]
    public void ForProduct_RoutesAKeyThatDoesNotMatchAKnownDocumentTypeIntoCustomRequired()
    {
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("not-a-real-document-type", "Something", "Reason", IsRequired: true, "intimation_and_reference", false),
            new ProductDocumentRequirement("ClaimForm", "Claim form", "Always needed", IsRequired: true, "intimation_and_reference", false),
        ]);

        var rules = DocumentChecklistRules.ForProduct(product);

        Assert.NotNull(rules);
        Assert.Single(rules!.RequiredDocuments);
        Assert.Contains(DocumentType.ClaimForm, rules.RequiredDocuments);
        Assert.Contains(rules.CustomRequired, entry => entry.Key == "not-a-real-document-type");
    }

    [Fact]
    public void ForProduct_ReturnsNullWhenTheMatrixIsEmpty()
    {
        var product = BaseProduct([]);

        var rules = DocumentChecklistRules.ForProduct(product);

        Assert.Null(rules);
    }

    [Fact]
    public void ForProduct_TreatsAnUnrecognizedKeyAsACustomRequiredDocumentInsteadOfDroppingIt()
    {
        var product = BaseProduct(
        [
            new ProductDocumentRequirement("totally-unknown-key", "Something", "Reason", IsRequired: true, "intimation_and_reference", false),
        ]);

        var rules = DocumentChecklistRules.ForProduct(product);

        Assert.NotNull(rules);
        Assert.Empty(rules!.RequiredDocuments);
        Assert.Contains(rules.CustomRequired, entry => entry.Key == "totally-unknown-key" && entry.Label == "Something");
    }

    private static Product BaseProduct(IReadOnlyList<ProductDocumentRequirement> documentMatrix) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = Scope,
            Version = 1,
            Name = "Motor Comprehensive",
            ProductClass = "Motor",
            Status = ProductStatus.Published,
            Stages = DefaultClaimStageLibrary.Default(),
            DocumentMatrix = documentMatrix,
            UpdatedByUserId = "admin-1",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Test setup.",
        };
}
