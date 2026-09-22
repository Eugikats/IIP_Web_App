using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Tests.Documents;

public sealed class DocumentClassificationAccuracyTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-quality", "workspace-claims");

    [Fact]
    public void FewerThanMinimumSamples_ReportsCountsButNoAccuracyFigure()
    {
        var examples = Enumerable.Range(0, 3).Select(_ => Example(DocumentType.GarageEstimate, DocumentType.GarageEstimate)).ToArray();

        var report = DocumentClassificationAccuracy.ByDocumentType(examples);

        var row = Assert.Single(report);
        Assert.Equal(DocumentType.GarageEstimate, row.DocumentType);
        Assert.Equal(3, row.SampleCount);
        Assert.Null(row.Accuracy);
    }

    [Fact]
    public void AtOrAboveMinimumSamples_ComputesAccuracyFromCorrectedVsPredicted()
    {
        var examples = new[]
        {
            Example(DocumentType.PoliceAbstract, DocumentType.PoliceAbstract),
            Example(DocumentType.PoliceAbstract, DocumentType.PoliceAbstract),
            Example(DocumentType.PoliceAbstract, DocumentType.PoliceAbstract),
            Example(DocumentType.PoliceAbstract, DocumentType.PoliceAbstract),
            Example(DocumentType.PoliceAbstract, DocumentType.Invoice), // the classifier got this one wrong
        };

        var report = DocumentClassificationAccuracy.ByDocumentType(examples);

        var row = Assert.Single(report);
        Assert.Equal(5, row.SampleCount);
        Assert.Equal(4, row.CorrectCount);
        Assert.Equal(0.8m, row.Accuracy);
    }

    [Fact]
    public void FieldCorrectionExamples_AreExcludedFromTheClassificationSignal()
    {
        var examples = Enumerable.Range(0, 5)
            .Select(_ => Example(DocumentType.Invoice, DocumentType.Invoice, DocumentLabelSource.FieldCorrection))
            .ToArray();

        var report = DocumentClassificationAccuracy.ByDocumentType(examples);

        Assert.Empty(report);
    }

    [Fact]
    public void ConfirmedWithoutCorrection_CountsAsACorrectPrediction()
    {
        var examples = Enumerable.Range(0, 5)
            .Select(_ => Example(DocumentType.CustomerId, DocumentType.CustomerId, DocumentLabelSource.ConfirmedWithoutCorrection))
            .ToArray();

        var report = DocumentClassificationAccuracy.ByDocumentType(examples);

        var row = Assert.Single(report);
        Assert.Equal(5, row.CorrectCount);
        Assert.Equal(1m, row.Accuracy);
    }

    private static DocumentLabelledExample Example(
        DocumentType predicted,
        DocumentType confirmed,
        DocumentLabelSource source = DocumentLabelSource.Reclassification) =>
        new(
            Guid.NewGuid(),
            Scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            predicted,
            0.6m,
            confirmed,
            new Dictionary<string, string>(),
            source,
            "officer-1",
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));
}
