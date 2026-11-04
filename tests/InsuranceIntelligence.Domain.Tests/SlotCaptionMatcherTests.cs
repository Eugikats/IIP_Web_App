using InsuranceIntelligence.Domain.Documents;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>A file sent on WhatsApp with a caption that names one slot goes in that slot; anything less sure goes to the
/// recogniser and then to a person (build-loop item 27).</summary>
public sealed class SlotCaptionMatcherTests
{
    private static ResolvedDocumentSlot Slot(string key, string label, string description = "", int maxFiles = 1, DocumentKind kind = DocumentKind.Document) =>
        new(key, label, description, true, kind, maxFiles > 1, maxFiles, DocumentSlotDefaults.FormatsFor(kind));

    private static readonly IReadOnlyList<ResolvedDocumentSlot> Slots =
    [
        Slot("PoliceAbstract", "the police report of the accident"),
        Slot("MedicalReport", "the medical form from the clinic or hospital"),
        Slot("CustomerId", "your national ID"),
        Slot("PhotoEvidence", "photos of the motorcycle or the injury", maxFiles: 10, kind: DocumentKind.Evidence),
    ];

    [Theory]
    [InlineData("police report", "PoliceAbstract")]
    [InlineData("Police reports", "PoliceAbstract")]
    [InlineData("here is the medical form", "MedicalReport")]
    [InlineData("my national ID", "CustomerId")]
    [InlineData("PoliceAbstract", "PoliceAbstract")]
    public void ACaptionThatNamesOneSlotPicksIt(string caption, string expected)
    {
        Assert.Equal(expected, SlotCaptionMatcher.Match(caption, Slots, [])?.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("please see attached")]
    [InlineData("report")]
    [InlineData("hospital police")]
    [InlineData("driving licence")]
    public void ACaptionThatIsEmptyVagueOrAmbiguousPicksNothing(string? caption)
    {
        // "report" is in two slots; "hospital police" mixes two; nothing names the licence.
        Assert.Null(SlotCaptionMatcher.Match(caption, Slots, []));
    }

    [Fact]
    public void ASlotThatAllowsSeveralFilesIsNeverPickedByCaption_SoPageOrderStaysWithTheRecogniser()
    {
        Assert.Null(SlotCaptionMatcher.Match("photos of the motorcycle", Slots, []));
    }

    [Fact]
    public void ASlotThatIsAlreadyFilledIsNotPickedAgain()
    {
        var filled = new CaseDocument
        {
            DocumentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Scope = InsuranceIntelligence.Domain.Common.TenantScope.Create("t", "w"),
            OriginalFileName = "police.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 3,
            ObjectKey = "k",
            DocumentType = DocumentType.Unknown,
            DocumentTypeKey = "PoliceAbstract",
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "test", DateTimeOffset.UtcNow),
            Extraction = new DocumentExtractionResult(new Dictionary<string, string>(), new Dictionary<string, decimal>(), "test", DateTimeOffset.UtcNow, false, null, null),
            ValidationWarnings = [],
            UploadedByUserId = "u",
            UploadedAt = DateTimeOffset.UtcNow,
            CorrelationId = "c",
        };

        Assert.Null(SlotCaptionMatcher.Match("police report", Slots, [filled]));
    }
}
