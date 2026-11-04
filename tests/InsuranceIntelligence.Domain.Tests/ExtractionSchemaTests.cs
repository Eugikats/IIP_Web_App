using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class ExtractionSchemaTests
{
    [Theory]
    [InlineData("field.ReportNumber", "reportnumber")]
    [InlineData("FIELD.ReportNumber", "reportnumber")]
    [InlineData("ReportNumber", "reportnumber")]
    [InlineData("  field.Total ", "total")]
    public void ExtractedKeysMatchWithoutTheProviderPrefixAndWithoutCase(string key, string expected)
    {
        Assert.Equal(expected, ExtractionSchemas.Normalise(key));
    }

    [Fact]
    public void OnlyMappingsThatAreCertainlyRightAreBuiltIn()
    {
        var medical = ExtractionSchemas.Defaults(DocumentType.MedicalReport);

        Assert.Contains(medical, spec => spec.ExtractedKey == "PatientName" && spec.ClaimFieldKey == "patientName");
        Assert.Contains(medical, spec => spec.ExtractedKey == "TreatmentDate" && spec.ClaimFieldKey == "treatmentDate");
        foreach (var type in Enum.GetValues<DocumentType>().Where(type => type != DocumentType.MedicalReport))
        {
            Assert.Empty(ExtractionSchemas.Defaults(type));
        }
    }

    [Fact]
    public void AClaimsProvenanceSaysWhoStandsBehindAnAnswer()
    {
        var provenance = new FieldProvenance("Document", "officer-1", DateTimeOffset.UtcNow, Guid.NewGuid(), 2, 0.87m, CorrectedByPerson: true);

        Assert.Equal("Document", provenance.Source);
        Assert.Equal(2, provenance.Page);
        Assert.True(provenance.CorrectedByPerson);
    }
}
