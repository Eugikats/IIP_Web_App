using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Tests.Azure;

public sealed class AzureDocumentIntelligenceMappingTests
{
    [Theory]
    [InlineData("invoice", DocumentType.Invoice)]
    [InlineData("receipt", DocumentType.Invoice)]
    [InlineData("medicalReport", DocumentType.MedicalReport)]
    [InlineData("identityDocument", DocumentType.CustomerId)]
    public void ProviderLabel_MapsToSupportedDocumentType(string providerType, DocumentType expected)
    {
        var result = AzureDocumentIntelligenceMapping.MapClassification(providerType, 0.92f);

        Assert.Equal(expected, result.DocumentType);
        Assert.Equal(0.92m, result.Confidence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("generalDocument")]
    [InlineData("policeReport")]
    public void UnknownProviderLabel_DoesNotInferFromFilenameOrInventConfidence(string? providerType)
    {
        var result = AzureDocumentIntelligenceMapping.MapClassification(providerType, 0.99f);

        Assert.Equal(DocumentType.Unknown, result.DocumentType);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact]
    public void MissingProviderConfidence_RemainsZero()
    {
        var result = AzureDocumentIntelligenceMapping.MapClassification("invoice", null);

        Assert.Equal(DocumentType.Invoice, result.DocumentType);
        Assert.Equal(0m, result.Confidence);
    }

    [Theory]
    [InlineData(-0.2f, 0d)]
    [InlineData(0.6f, 0.6d)]
    [InlineData(1.4f, 1d)]
    public void ProviderConfidence_IsClampedToRange(float input, double expected)
    {
        Assert.Equal((decimal)expected, AzureDocumentIntelligenceMapping.NormalizeConfidence(input));
    }
}
