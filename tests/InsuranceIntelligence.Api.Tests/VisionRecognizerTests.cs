using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Local.Mock;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The recognisers themselves: how a vision model's one-line answer is read, and how the
/// local stand-in behaves, without any network or model.</summary>
public sealed class VisionRecognizerTests
{
    private static readonly IReadOnlyList<DocumentCatalogueEntry> Catalogue =
    [
        new("PoliceAbstract", "Police report", "Confirms the incident was reported."),
        new("LogBook", "Vehicle log book", "The registration document."),
    ];

    [Fact]
    public void ACleanAnswerIsReadIntoATypeConfidenceAndReason()
    {
        var recognition = AzureOpenAiVisionDocumentRecognizer.Parse(
            "{\"typeKey\":\"logbook\",\"confidence\":0.93,\"reason\":\"It has a registration number and chassis number.\"}", Catalogue, "gpt-x");

        Assert.Equal("LogBook", recognition.TypeKey);
        Assert.Equal(0.93m, recognition.Confidence);
        Assert.True(recognition.IsConfident);
        Assert.Equal("gpt-x", recognition.ModelVersion);
    }

    [Fact]
    public void AnAnswerWrappedInProseOrFencesIsStillRead()
    {
        var recognition = AzureOpenAiVisionDocumentRecognizer.Parse(
            "Here is my answer:\n```json\n{\"typeKey\":\"PoliceAbstract\",\"confidence\":0.85,\"reason\":\"Police stamp.\"}\n```", Catalogue, null);

        Assert.Equal("PoliceAbstract", recognition.TypeKey);
    }

    [Theory]
    [InlineData("{\"typeKey\":\"Passport\",\"confidence\":0.99,\"reason\":\"It is a passport.\"}")]
    [InlineData("{\"typeKey\":null,\"confidence\":0.1,\"reason\":\"Cannot tell.\"}")]
    [InlineData("I think this is a log book.")]
    [InlineData("{ not json }")]
    [InlineData("")]
    public void ATypeNotInTheCatalogueOrAnUnreadableAnswerIsNeverATypeAndNeverConfident(string answer)
    {
        var recognition = AzureOpenAiVisionDocumentRecognizer.Parse(answer, Catalogue, null);

        Assert.Null(recognition.TypeKey);
        Assert.False(recognition.IsConfident);
        Assert.False(string.IsNullOrWhiteSpace(recognition.Reason));
    }

    [Fact]
    public void AnImpossibleConfidenceIsClampedAndEmDashesNeverReachAPerson()
    {
        var recognition = AzureOpenAiVisionDocumentRecognizer.Parse(
            "{\"typeKey\":\"LogBook\",\"confidence\":7,\"reason\":\"Registration — chassis number\"}", Catalogue, null);

        Assert.Equal(1m, recognition.Confidence);
        Assert.DoesNotContain('—', recognition.Reason);
    }

    [Fact]
    public async Task ThePictureNeedsToBeAPictureForTheRealModel()
    {
        var recogniser = new AzureOpenAiVisionDocumentRecognizer(
            new InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions
            {
                AzureOpenAI = new() { Endpoint = "https://example.invalid", ChatDeployment = "vision" },
            },
            new NoPreview(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureOpenAiVisionDocumentRecognizer>.Instance,
            new HttpClient(new RefuseEverything()));

        var recognition = await recogniser.RecognizeAsync(
            new DocumentRecognitionRequest("letter.pdf", "application/pdf", _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])), Catalogue));

        Assert.Null(recognition.TypeKey);
        Assert.Contains("person will look", recognition.Reason);
    }

    [Fact]
    public async Task TheMockReadsAMarkerALabelOrNothing()
    {
        var mock = new MockDocumentRecognizer();
        static DocumentRecognitionRequest Request(string text) =>
            new("f.pdf", "application/pdf", _ => Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text))), Catalogue);

        var marker = await mock.RecognizeAsync(Request("IIP-MOCK-DOC:LogBook"));
        var weak = await mock.RecognizeAsync(Request("IIP-MOCK-DOC:LogBook IIP-MOCK-CONFIDENCE:0.4"));
        var label = await mock.RecognizeAsync(Request("This is a Police Report from the station"));
        var unknown = await mock.RecognizeAsync(Request("IIP-MOCK-DOC:Passport"));
        var nothing = await mock.RecognizeAsync(Request("lorem ipsum"));

        Assert.True(marker.IsConfident);
        Assert.Equal("LogBook", marker.TypeKey);
        Assert.False(weak.IsConfident);
        Assert.Equal("PoliceAbstract", label.TypeKey);
        Assert.Null(unknown.TypeKey);
        Assert.Null(nothing.TypeKey);
        Assert.All(new[] { marker, weak, label, unknown, nothing }, result => Assert.Equal("Mock", result.ProviderName));
    }

    private sealed class NoPreview : IImageDerivativeProvider
    {
        public Task<ImageDerivative?> CreatePreviewAsync(Stream image, string fileName, int maxEdgePixels, CancellationToken cancellationToken = default) =>
            Task.FromResult<ImageDerivative?>(null);
    }

    private sealed class RefuseEverything : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No network call should be made for a file that is not a picture.");
    }
}
