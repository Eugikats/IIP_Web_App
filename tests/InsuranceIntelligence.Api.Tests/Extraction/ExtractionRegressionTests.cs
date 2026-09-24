using System.Text.Json;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Tests.Extraction;

/// <summary>
/// Golden-fixture regression suite. Each fixture file contains a captured
/// DocumentExtractionResult. Tests fail with a descriptive message when a field
/// disappears, a confidence drops below threshold, or the recorded ModelVersion
/// changes, signalling that the upstream model or prompt has changed in a way
/// that needs human review before merging.
/// </summary>
public sealed class ExtractionRegressionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string FixturesDirectory = "fixtures/extraction";

    private static GoldenFixture LoadFixture(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Golden fixture not found: {path}");
        var json = File.ReadAllText(path);
        var fixture = JsonSerializer.Deserialize<GoldenFixture>(json, JsonOptions);
        Assert.NotNull(fixture);
        return fixture;
    }

    [Theory]
    [InlineData("motor-claim-form-golden.json", "sourceFileName")]
    [InlineData("motor-claim-form-golden.json", "pageCount")]
    [InlineData("motor-claim-form-golden.json", "contentType")]
    [InlineData("police-report-golden.json", "sourceFileName")]
    [InlineData("police-report-golden.json", "pageCount")]
    public void GoldenFixture_ContainsExpectedField(string fixtureFile, string fieldName)
    {
        var fixture = LoadFixture(fixtureFile);
        Assert.True(
            fixture.Fields.ContainsKey(fieldName),
            $"Field '{fieldName}' is missing from fixture '{fixtureFile}'. " +
            $"Available fields: {string.Join(", ", fixture.Fields.Keys)}");
    }

    [Theory]
    [InlineData("motor-claim-form-golden.json", "sourceFileName", 0.5)]
    [InlineData("motor-claim-form-golden.json", "pageCount", 0.5)]
    [InlineData("police-report-golden.json", "sourceFileName", 0.5)]
    public void GoldenFixture_FieldConfidenceAboveThreshold(string fixtureFile, string fieldName, double threshold)
    {
        var fixture = LoadFixture(fixtureFile);
        Assert.True(
            fixture.FieldConfidences.TryGetValue(fieldName, out var confidence),
            $"Confidence for field '{fieldName}' is missing from fixture '{fixtureFile}'.");
        Assert.True(
            (double)confidence >= threshold,
            $"Field '{fieldName}' confidence {confidence:F2} dropped below threshold {threshold:F2} in '{fixtureFile}'. " +
            "Review the extraction model version or prompt before merging.");
    }

    [Theory]
    [InlineData("motor-claim-form-golden.json")]
    [InlineData("police-report-golden.json")]
    public void GoldenFixture_ModelVersionIsRecorded(string fixtureFile)
    {
        var fixture = LoadFixture(fixtureFile);
        Assert.False(
            string.IsNullOrWhiteSpace(fixture.ModelVersion),
            $"Fixture '{fixtureFile}' has no ModelVersion recorded. " +
            "Re-capture the fixture after configuring Document Intelligence.");
    }

    [Theory]
    [InlineData("motor-claim-form-golden.json")]
    [InlineData("police-report-golden.json")]
    public void GoldenFixture_ModelVersionMatchesBaseline(string fixtureFile)
    {
        var fixture = LoadFixture(fixtureFile);
        Assert.False(
            string.IsNullOrWhiteSpace(fixture.BaselineModelVersion),
            $"Fixture '{fixtureFile}' has no BaselineModelVersion set. Add it after first successful capture.");
        Assert.Equal(
            fixture.BaselineModelVersion,
            fixture.ModelVersion,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Minimal projection of a stored DocumentExtractionResult for golden tests.</summary>
    private sealed class GoldenFixture
    {
        public Dictionary<string, string> Fields { get; init; } = [];
        public Dictionary<string, decimal> FieldConfidences { get; init; } = [];
        public string? ModelVersion { get; init; }
        /// <summary>Set after the first successful real extraction; triggers failure on unexpected model change.</summary>
        public string? BaselineModelVersion { get; init; }
    }
}
